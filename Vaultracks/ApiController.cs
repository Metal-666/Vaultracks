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
public class ApiController : ControllerBase {

	protected virtual ILogger Logger { get; set; }
	protected virtual WebSocketController WsController { get; set; }

	public ApiController(ILogger<ApiController> logger, WebSocketController wsController) {

		Logger = logger;
		WsController = wsController;

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

		UserAuth? userAuth = UserAuth.ParseBase64(base64auth);

		if(userAuth == null) {

			return BadRequest("Please use HTTP Basic Authentication to pass username and database key!");

		}

		SQLiteAsyncConnection? db = await DatabaseManager.GetDb(userAuth, true);

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

		EventBus.LocationEvents.OnNext((userAuth, location));

		return Ok("[]");

	}

	/*[HttpGet("location/latest")]
	public virtual async Task<ActionResult<Location>> GetLatestLocation([FromHeader(Name = "Authorization")] string base64auth) {

		UserAuth? userAuth = UserAuth.Parse(base64auth);

		if(userAuth == null) {

			return BadRequest("Please use HTTP Basic Authentication to pass username and database key!");

		}

		SQLiteAsyncConnection? db = await GetDb(userAuth, false);

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

	}*/

}

public static class PayloadType {

	public const string Location = "location";

}