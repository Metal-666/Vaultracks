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

	public static ConcurrentDictionary<string, SQLiteAsyncConnection> ActiveDatabaseConnections { get; protected set; } =
		new();

	protected virtual ILogger Logger { get; set; }

	public ApiController(ILogger<ApiController> logger) {

		Logger = logger;

	}

	[HttpPost("postMessage")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
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

		const string BasicAuthPrefix = "Basic ";

		if(!base64auth.StartsWith(BasicAuthPrefix)) {

			return BadRequest("Please use HTTP Basic Authentication to pass the database key!");

		}

		string[] auth =
			Encoding.UTF8
					.GetString(Convert.FromBase64String(base64auth[BasicAuthPrefix.Length..]))
					.Split(':');

		(string Username, string DatabaseKey) = (auth[0], auth[1]);

		if(!ActiveDatabaseConnections.TryGetValue(Username, out SQLiteAsyncConnection? db)) {

			db = new(new SQLiteConnectionString(GetDatabaseFilePath(Username),
																				SQLiteOpenFlags.ReadWrite |
																							SQLiteOpenFlags.Create |
																							SQLiteOpenFlags.FullMutex,
																				true,
																				DatabaseKey));

			if(!ActiveDatabaseConnections.TryAdd(Username, db)) {

				await db.CloseAsync();

				return Problem("Failed to initialize the database, please try again!");

			}

		}

		int rowsWritten;

		try {

			await db.CreateTableAsync<Location>();

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

	public static string GetDatabaseFilePath(string username) =>
		Path.Combine(DataDirectory, $"{username}.db");

}

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