using SQLite;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vaultracks;

public class Location {

	[JsonIgnore, Ignore]
	public virtual long? CreatedAtWithTimestampFallback =>
		CreatedAt ?? Timestamp;

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

	// We need to store Latitude and Longitude as strings, because SQLite is limited to 8-bit floats,
	// which are not precise enough to store location data.
	[JsonPropertyName("lat"), Ignore]
	public virtual decimal? Latitude { get; set; }

	[JsonIgnore]
	public virtual string? LatitudeString {

		get =>
			Latitude?.ToString();
		set =>
			Latitude =
				value == null ?
					null :
					decimal.Parse(value);

	}

	[JsonPropertyName("lon"), Ignore]
	public virtual decimal? Longitude { get; set; }

	[JsonIgnore]
	public virtual string? LongitudeString {

		get =>
			Longitude?.ToString();
		set =>
			Longitude =
				value == null ?
					null :
					decimal.Parse(value);

	}

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

	// Similarly to lat/lon, we store lists as strings.
	[JsonPropertyName("inregions"), Ignore]
	public virtual List<string>? InRegions { get; set; }

	[JsonIgnore]
	public virtual string? InRegionsString {

		get =>
			InRegions == null ?
				null :
				JsonSerializer.Serialize(InRegions);
		set =>
			InRegions =
				value == null ?
					null :
					JsonSerializer.Deserialize<List<string>>(value);

	}

	[JsonPropertyName("inrids"), Ignore]
	public virtual List<string>? InRegionIds { get; set; }

	[JsonIgnore]
	public virtual string? InRegionIdsString {

		get =>
			InRegionIds == null ?
				null :
				JsonSerializer.Serialize(InRegionIds);
		set =>
			InRegionIds =
				value == null ?
					null :
					JsonSerializer.Deserialize<List<string>>(value);

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
	public const string WiFi = "w";
	public const string Mobile = "m";

}