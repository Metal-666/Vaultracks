using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using SQLite;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Vaultracks;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase {

	public const string DataDirectory = "/data";

	public static ConcurrentDictionary<DBAccess, SQLiteAsyncConnection> ActiveDatabaseConnections { get; protected set; } =
		new();

	protected virtual ILogger Logger { get; set; }

	public ApiController(ILogger<ApiController> logger) {

		Logger = logger;

	}

	[HttpPost("message")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<ActionResult> PostMessage([FromHeader(Name = "Authorization")] string base64auth) {

		using StreamReader bodyReader = new(Request.Body);

		JsonNode? payload = null;

		try {

			payload = JsonNode.Parse(await bodyReader.ReadToEndAsync());

		}

		catch { }

		JsonNode? payloadType = payload?["_type"];

		if(payloadType == null ||
			payloadType.GetValueKind() != JsonValueKind.String ||
			payloadType.GetValue<string>() != PayloadType.Location) {

			return BadRequest("Failed to process payload (not JSON/invalid '_type'/unsupported '_type')!");

		}

		Location? location =
			payload.Deserialize<Location>();

		if(location == null) {

			return BadRequest("Payload parsing failed!");

		}

		DBAccess? dbAccess = ParseAuth(base64auth);

		if(dbAccess == null) {

			return BadRequest("Please use HTTP Basic Authentication to pass username and database key!");

		}

		SQLiteAsyncConnection? db = await GetDb(dbAccess, true);

		if(db == null) {

			return Problem("Failed to initialize the database, please try again!");

		}

		int rowsWritten;

		try {

			rowsWritten = await db.InsertAsync(location);

		}

		catch(Exception e) {

			Logger.LogError("Failed to write Location to db: {message}", e.Message);

			return Problem("Database error :(");

		}

		if(rowsWritten != 1) {

			return Problem($"Failed to write to database: expected to write 1 row, written: {rowsWritten}");

		}

		return Ok("[]");

	}

	[HttpGet("location/latest")]
	public virtual async Task<ActionResult<Location>> GetLatestLocation([FromHeader(Name = "Authorization")] string base64auth) {

		DBAccess? dbAccess = ParseAuth(base64auth);

		if(dbAccess == null) {

			return BadRequest("Please use HTTP Basic Authentication to pass username and database key!");

		}

		SQLiteAsyncConnection? db = await GetDb(dbAccess, false);

		if(db == null) {

			return StatusCode(StatusCodes.Status500InternalServerError, "Failed to open the database! Is the username/password correct? Does the db exist?");

		}

		Location? location =
			await db.Table<Location>()
					.OrderByDescending(location =>
													location.CreatedAt)
					.FirstOrDefaultAsync();

		if(location == null) {

			return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve location!");

		}

		return location;

	}

	protected virtual DBAccess? ParseAuth(string base64auth) {

		const string BasicAuthPrefix = "Basic ";

		if(!base64auth.StartsWith(BasicAuthPrefix)) {

			return null;

		}

		string[] auth =
			Encoding.UTF8
					.GetString(Convert.FromBase64String(base64auth[BasicAuthPrefix.Length..]))
					.Split(':');

		return new(auth[0], auth[1]);

	}

	protected virtual async Task<SQLiteAsyncConnection?> GetDb(DBAccess dbAccess, bool createIfDoesNotExist) {

		try {

			if(!ActiveDatabaseConnections.TryGetValue(dbAccess, out SQLiteAsyncConnection? db)) {

				SQLiteOpenFlags flags =
					SQLiteOpenFlags.ReadWrite |
						SQLiteOpenFlags.FullMutex;

				if(createIfDoesNotExist) {

					flags |= SQLiteOpenFlags.Create;

				}

				db = new(new SQLiteConnectionString(GetDatabaseFilePath(dbAccess.Username),
																					flags,
																					true,
																					dbAccess.DatabaseKey));

				await db.CreateTableAsync<Location>();

				if(!ActiveDatabaseConnections.TryAdd(dbAccess, db)) {

					await db.CloseAsync();

					return null;

				}

			}

			return db;

		}

		catch {

			return null;

		}

	}

	public static string GetDatabaseFilePath(string username) =>
		Path.Combine(DataDirectory, $"{username}.db");

}

public record DBAccess(string Username, string DatabaseKey);

public static class PayloadType {

	public const string Location = "location";

}

public class Location {

	[PrimaryKey, AutoIncrement, JsonIgnore]
	public virtual long Id { get; set; }

	public virtual string? BSSID { get; set; }

	public virtual string? SSID { get; set; }

	[JsonPropertyName("_id"), Ignore]
	public virtual string? OTId { get; set; }

	[JsonPropertyName("acc")]
	public virtual int? Accuracy { get; set; }

	[JsonPropertyName("alt")]
	public virtual int? Altitude { get; set; }

	[JsonPropertyName("batt")]
	public virtual int? Battery { get; set; }

	[JsonPropertyName("bs")]
	public virtual BatteryStatus? BatteryStatus { get; set; }

	[JsonPropertyName("cog")]
	public virtual int? CourseOverGround { get; set; }

	[JsonPropertyName("conn")]
	public virtual string? ConnectionType { get; set; }

	[JsonPropertyName("created_at")]
	public virtual long? CreatedAt { get; set; }

	[JsonPropertyName("lat")]
	public virtual decimal? Latitude { get; set; }

	[JsonPropertyName("lon")]
	public virtual decimal? Longitude { get; set; }

	[JsonPropertyName("rad")]
	public virtual long? RadiusAroundRegion { get; set; }

	[JsonPropertyName("t")]
	public virtual string? Trigger { get; set; }

	[JsonPropertyName("m")]
	public virtual MonitoringMode? MonitoringMode { get; set; }

	[JsonPropertyName("tid")]
	public virtual string? TrackerID { get; set; }

	[JsonPropertyName("topic")]
	public virtual string? Topic { get; set; }

	[JsonPropertyName("tst")]
	public virtual long? Timestamp { get; set; }

	[JsonPropertyName("vac")]
	public virtual int? VerticalAccuracy { get; set; }

	[JsonPropertyName("vel")]
	public virtual int? Velocity { get; set; }

	[JsonPropertyName("p")]
	public virtual float? Pressure { get; set; }

	[JsonPropertyName("poi")]
	public virtual string? PointOfInterest { get; set; }

	[JsonPropertyName("tag")]
	public virtual string? Tag { get; set; }

	public virtual string? InRegions => SubObjectAsString("inregions");

	public virtual string? InRegionIds => SubObjectAsString("inrids");

	[JsonExtensionData, Ignore]
	public virtual Dictionary<string, JsonElement>? ExtensionData { get; set; }

	public virtual string? SubObjectAsString(string key) {

		if(ExtensionData == null) {

			return null;

		}

		if(!ExtensionData.TryGetValue(key, out JsonElement element)) {

			return null;

		}

		if(element.ValueKind == JsonValueKind.Null) {

			return null;

		}

		return element.GetRawText();

	}

}

public enum BatteryStatus {

	Unknown,
	Unplugged,
	Charging,
	Full

}

public enum MonitoringMode {

	Quiet = -1,
	Manual = 0,
	Significant = 1,
	Move = 2

}

public static class ConnectionType {

	public const string Offline = "o";
	public const string Wifi = "w";
	public const string Mobile = "m";

}