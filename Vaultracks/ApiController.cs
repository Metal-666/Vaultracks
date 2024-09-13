using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using SQLite;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace Vaultracks;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase {

	public const string DataDirectory = "/data";

	public virtual Dictionary<string, SQLiteConnection> ActiveDatabaseConnections { get; protected set; } =
		new();

	protected virtual ILogger Logger { get; set; }

	public ApiController(ILogger<ApiController> logger) {

		Logger = logger;

	}

	/*[HttpPost("initializeDatabase")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual ActionResult InitializeDatabase([FromQuery] string key) {

		if(DB != null || Program.DatabaseExists) {

			return BadRequest("Database already exists!");

		}

		DB =
			new(new SQLiteConnectionString(Program.DatabaseFilePath,
																		SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create,
																		true,
																		key));

		DB.CreateTable<Location>();

		return Ok();

	}*/

	[HttpPost("postMessage")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual ActionResult PostMessage([FromHeader(Name = "Authorization")] string base64auth,
											[FromBody] OwnTracksPayload payload) {

		if(payload.Type == null) {

			return BadRequest("Message type cannot be null!");

		}

		const string BasicAuthPrefix = "Basic ";

		if(!base64auth.StartsWith(BasicAuthPrefix)) {

			return BadRequest("Please use HTTP Basic Authentication to pass the database key!");

		}

		string[] auth = Encoding.UTF8.GetString(Convert.FromBase64String(base64auth[BasicAuthPrefix.Length..])).Split(':');

		(string Username, string DatabaseKey) = (auth[0], auth[1]);

		if(!ActiveDatabaseConnections.TryGetValue(Username, out SQLiteConnection? db)) {

			db = new(new SQLiteConnectionString(GetDatabaseFilePath(Username),
																			SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create,
																			true,
																			DatabaseKey));

			ActiveDatabaseConnections.Add(Username, db);

		}

		db.CreateTable<Location>();

		db.Insert(new Location() {

			BSSID = payload.BSSID,
			SSID = payload.SSID,
			OTId = payload.Id,
			Accuracy = payload.Accuracy,
			Altitude = payload.Altitude,
			Battery = payload.Battery,
			BatteryStatus = payload.BatteryStatus,
			ConnectionType = payload.ConnectionType,
			CreatedAt = payload.CreatedAt,
			Latitude = payload.Latitude,
			Longitude = payload.Longitude,
			MonitoringMode = payload.MonitoringMode,
			TrackerID = payload.TrackerID,
			Topic = payload.Topic,
			Timestamp = payload.Timestamp,
			VerticalAccuracy = payload.VerticalAccuracy,
			Velocity = payload.Velocity

		});

		/*if(DB == null && !Program.DatabaseExists) {

			return BadRequest("Database was not found!");

		}

		Logger.LogInformation(authorization);

		DB ??=
			new(new SQLiteConnectionString(Program.DatabaseFilePath,
																		SQLiteOpenFlags.ReadWrite,
																		true,
																		authorization));*/

		return Ok("[]");

	}

	public static string GetDatabaseFilePath(string username) =>
		 Path.Combine(DataDirectory, $"{username}.db");

}

public class OwnTracksPayload {

	[JsonPropertyName("_type")]
	public virtual string? Type { get; set; }

	public virtual string BSSID { get; set; } = "00:00:00:00:00:00:00:00";

	public virtual string SSID { get; set; } = "";

	[JsonPropertyName("_id")]
	public virtual string Id { get; set; } = "";

	[JsonPropertyName("acc")]
	public virtual int Accuracy { get; set; } = -1;

	[JsonPropertyName("alt")]
	public virtual int Altitude { get; set; } = int.MinValue;

	[JsonPropertyName("batt")]
	public virtual int Battery { get; set; } = -1;

	[JsonPropertyName("bs")]
	public virtual BatteryStatus? BatteryStatus { get; set; }

	[JsonPropertyName("conn")]
	public virtual string ConnectionType { get; set; } = "";

	[JsonPropertyName("created_at")]
	public virtual long CreatedAt { get; set; } = -1;

	[JsonPropertyName("lat")]
	public virtual decimal Latitude { get; set; } = 0;

	[JsonPropertyName("lon")]
	public virtual decimal Longitude { get; set; } = 0;

	[JsonPropertyName("m")]
	public virtual MonitoringMode? MonitoringMode { get; set; }

	[JsonPropertyName("tid")]
	public virtual string TrackerID { get; set; } = "";

	[JsonPropertyName("topic")]
	public virtual string Topic { get; set; } = "";

	[JsonPropertyName("tst")]
	public virtual long Timestamp { get; set; } = -1;

	[JsonPropertyName("vac")]
	public virtual int VerticalAccuracy { get; set; } = -1;

	[JsonPropertyName("vel")]
	public virtual int Velocity { get; set; } = -1;

}

public static class PayloadType {

	public const string Location = "location";

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

public class Location {

	[PrimaryKey, AutoIncrement]
	public virtual long Id { get; set; }
	public virtual string BSSID { get; set; } = "00:00:00:00:00:00:00:00";
	public virtual string SSID { get; set; } = "";
	public virtual string OTId { get; set; } = "";
	public virtual int Accuracy { get; set; } = -1;
	public virtual int Altitude { get; set; } = int.MinValue;
	public virtual int Battery { get; set; } = -1;
	public virtual BatteryStatus? BatteryStatus { get; set; }
	public virtual string ConnectionType { get; set; } = "";
	public virtual long CreatedAt { get; set; } = -1;
	public virtual decimal Latitude { get; set; } = 0;
	public virtual decimal Longitude { get; set; } = 0;
	public virtual MonitoringMode? MonitoringMode { get; set; }
	public virtual string TrackerID { get; set; } = "";
	public virtual string Topic { get; set; } = "";
	public virtual long Timestamp { get; set; } = -1;
	public virtual int VerticalAccuracy { get; set; } = -1;
	public virtual int Velocity { get; set; } = -1;

}