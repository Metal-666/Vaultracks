using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using SQLite;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Vaultracks;

[ApiController]
[Route("api")]
public class ApiController(ILogger<ApiController> logger) : ControllerBase {

	protected virtual ILogger Logger { get; set; } = logger;

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
			payloadType.GetValueKind() != JsonValueKind.String) {

			return BadRequest("Failed to process payload (not JSON/invalid '_type')!");

		}

		string payloadTypeValue = payloadType.GetValue<string>();

		if(payloadTypeValue != PayloadType.Location) {

			Logger.LogWarning("The payload type ({payloadType}) is unsupported, discarding!", payloadTypeValue);

			return Ok("[]");

		}

		Location? location =
			payload.Deserialize<Location>();

		if(location == null) {

			return BadRequest("Payload parsing failed!");

		}

		UserAuth? userAuth = UserAuth.ParseBase64(base64auth);

		if(userAuth == null) {

			return BadRequest("Please use HTTP Basic Authentication to pass username and database key!");

		}

		SQLiteAsyncConnection db;

		try {

			db = await DatabaseManager.GetDb(userAuth, true);

		}

		catch(Exception e) {

			return Problem($"Failed to initialize the database! ({e.Message})");

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

		EventBus.LocationEvents.OnNext((userAuth, location));

		return Ok("[]");

	}

}

public static class PayloadType {

	public const string Location = "location";

}