using SQLite;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vaultracks;

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