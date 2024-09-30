using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SQLite;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Timer = System.Timers.Timer;

namespace Vaultracks;

public class WebSocketController : ControllerBase {

	protected static readonly JsonSerializerOptions JsonSerializerOptions =
		new() {

			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull

		};

	protected virtual List<WebSocketConnection> Connections { get; set; } = new();

	protected virtual ILogger Logger { get; set; }

	public WebSocketController(ILogger<WebSocketController> logger, IHostApplicationLifetime lifetime) {

		Logger = logger;

		lifetime.ApplicationStopping.Register(async () => {

			Logger.LogInformation("Closing ws connections...");

			foreach(WebSocketConnection wsConnection in Connections) {

				await wsConnection.WebSocket
									.CloseAsync(WebSocketCloseStatus.NormalClosure,
												"Server is shutting down...",
												CancellationToken.None);

			}

		});

	}

	[HttpGet("/ws")]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task Ws([FromQuery] string username, [FromQuery] string databaseKey) {

		if(!HttpContext.WebSockets.IsWebSocketRequest) {

			Response.StatusCode = StatusCodes.Status400BadRequest;
			await Response.WriteAsync("Request is not a websocket request!");

			return;

		}

		UserAuth? userAuth =
			UserAuth.ParseUrlEncoded(username, databaseKey);

		using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

		if(userAuth == null) {

			await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation,
										"Failed to parse auth data!",
										CancellationToken.None);

			return;

		}

		SQLiteAsyncConnection db;

		try {

			db = await DatabaseManager.GetDb(userAuth, true);

		}

		catch(Exception e) {

			await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError,
										$"Failed to connect to the database! ({e.Message})",
										CancellationToken.None);

			return;

		}

		WebSocketConnection connection = new() {

			WebSocket = webSocket,
			UserAuth = userAuth

		};

		Connections.Add(connection);

		Logger.LogInformation("Established WS connection with client!");

		CancellationTokenSource cancellationTokenSource = new();

		WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure;
		string? closeStatusDescription = null;

		List<IDisposable> subscriptions = new();

		while(connection.WebSocket.State != WebSocketState.Closed) {

			Timer timeout = new(TimeSpan.FromSeconds(20));

			timeout.Elapsed +=
				(sender, args) =>
					cancellationTokenSource.Cancel();

			timeout.Start();

			byte[] buffer = new byte[1024 * 4];

			WebSocketReceiveResult result;

			try {

				result =
					await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
													cancellationTokenSource.Token);

			}

			catch {

				closeStatusDescription = "Connection timeout!";

				break;

			}

			timeout.Dispose();

			if(result.CloseStatus.HasValue) {

				break;

			}

			if(result.MessageType != WebSocketMessageType.Text) {

				await SendError(webSocket,
								"Invalid message type!");

				continue;

			}

			string jsonData =
				Encoding.UTF8.GetString(buffer,
										0,
										result.Count);

			Logger.LogInformation("Received WS message: {jsonData}", jsonData);

			WebSocketMessage? message = null;

			try {

				message = JsonSerializer.Deserialize<WebSocketMessage>(jsonData, JsonSerializerOptions);

			}

			catch(Exception e) {

				await SendError(webSocket,
								"Failed to deserialize message!",
								e.Message);

				continue;

			};

			if(message == null) {

				await SendError(webSocket,
								"Failed to parse message!");

				continue;

			}

			if(!message.Command.HasValue) {

				await SendError(webSocket,
								"Command was not provided!");

				continue;

			}

			switch(message.Command) {

				case Command.Error: {

					Logger.LogError("Received error from client: {errorMessage} {errorDescription}",
									message.ErrorMessage,
									message.ErrorDescription);

					break;

				}

				case Command.SubscribeToLocationUpdates: {

					if(connection.ReceiveLocationUpdates) {

						break;

					}

					connection.ReceiveLocationUpdates = true;

					IDisposable subscription =
						EventBus.LocationEvents
								.Where(locationEvent =>
													locationEvent.UserAuth == userAuth)
								.Select(locationEvent =>
													locationEvent.Location)
								.Scan<Location, (Location? Previous, Location Current)>((null, null!),
																						(accumulated,
																									currentLocation) =>
																										(accumulated.Current, currentLocation))
								.Where((locationPair) =>
													locationPair.Previous == null ||
														!locationPair.Current.CreatedAtWithTimestampFallback.HasValue ||
														!locationPair.Previous.CreatedAtWithTimestampFallback.HasValue ||
														locationPair.Current.CreatedAtWithTimestampFallback >
															locationPair.Previous.CreatedAtWithTimestampFallback)
								.Select(locationPair =>
													locationPair.Current)
								.Subscribe(async location => {

									await SendMessage(connection.WebSocket,
														new() {

															Command = Command.LocationUpdate,
															LocationJson = JsonSerializer.Serialize(location)

														});

								});

					subscriptions.Add(subscription);

					Location? location =
						await db.Table<Location>()
								.OrderByDescending(location =>
																location.Id)
								.ThenByDescending(location =>
															location.Timestamp)
								.ThenByDescending(location =>
															location.CreatedAt)
								.FirstOrDefaultAsync();

					if(location == null) {

						await SendError(connection.WebSocket,
										"Latest location not available");

						break;

					}

					await SendMessage(connection.WebSocket,
										new() {

											Command = Command.LocationUpdate,
											LocationJson = JsonSerializer.Serialize(location)

										});

					break;

				}

				case Command.RequestLocations: {

					// Unfortunately, the sqlite library I'm using is having trouble compiling sql queries from
					// linq, where functions or properties are used (this also includes nullable properties).
					// This is why the following code was unfolded, uglified, deconstructed and simplified to avoid using
					// .Value, .HasValue and other properties inside the linq statements, while also dealing
					// with the limited set of linq expressions, supported by the sql api.
					bool hasFromTimestamp = message.FromTimestamp.HasValue;
					bool hasToTimestamp = message.ToTimestamp.HasValue;

					int fromTimestamp = hasFromTimestamp ? message.FromTimestamp!.Value : 0;
					int toTimestamp = hasToTimestamp ? message.ToTimestamp!.Value : 0;

					List<Location> locationsWithCreatedAt =
						await db.Table<Location>()
								.Where(location =>
												location.CreatedAt != null)
								.Where(location =>
												(!hasFromTimestamp ||
													location.CreatedAt >= fromTimestamp) &&
												(!hasToTimestamp ||
													location.CreatedAt <= toTimestamp))
								.ToListAsync();

					List<Location> locationsWithTimestampOnly =
						await db.Table<Location>()
								.Where(location =>
												location.Timestamp != null &&
												location.CreatedAt == null)
								.Where(location =>
												(!hasFromTimestamp ||
													location.Timestamp >= fromTimestamp) &&
												(!hasToTimestamp ||
													location.Timestamp <= toTimestamp))
								.ToListAsync();

					IEnumerable<Location> locations =
						locationsWithCreatedAt.Concat(locationsWithTimestampOnly)
												.OrderByDescending(location =>
																				location.CreatedAtWithTimestampFallback);

					foreach(Location location in locations) {

						await SendMessage(connection.WebSocket,
											new() {

												Command = Command.RequestedLocation,
												LocationJson = JsonSerializer.Serialize(location)

											});

					}

					break;

				}

			}

		}

		Connections.Remove(connection);

		foreach(IDisposable subscription in subscriptions) {

			subscription.Dispose();

		}

		if(new[] {

			WebSocketState.Open,
			WebSocketState.CloseReceived,
			WebSocketState.CloseSent

		}.Contains(webSocket.State)) {

			await webSocket.CloseAsync(closeStatus,
										closeStatusDescription,
										CancellationToken.None);

		}

		webSocket.Dispose();

		Logger.LogInformation("WS connection closed!");

	}

	public virtual async Task SendMessage(WebSocket webSocket, WebSocketMessage message) {

		string jsonData = JsonSerializer.Serialize(message, JsonSerializerOptions);

		Logger.LogDebug("Sending WS message: {jsonData}", jsonData);

		if(webSocket.State != WebSocketState.Open) {

			Logger.LogWarning("Failed to send WS message: connection state is {state}!", webSocket.State);

			return;

		}

		await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonData)),
									WebSocketMessageType.Text,
									true,
									CancellationToken.None);

	}

	public virtual async Task SendError(WebSocket webSocket,
										string message,
										string? description = null) =>
		await SendMessage(webSocket,
							new() {

								Command = Command.Error,
								ErrorMessage = message,
								ErrorDescription = description

							});

}

public class WebSocketConnection {

	public required virtual WebSocket WebSocket { get; set; }
	public required virtual UserAuth UserAuth { get; set; }
	public virtual bool ReceiveLocationUpdates { get; set; } = false;

}

public class WebSocketMessage {

	public virtual Command? Command { get; set; }
	public virtual string? ErrorMessage { get; set; }
	public virtual string? ErrorDescription { get; set; }
	public virtual string? LocationJson { get; set; }
	public virtual int? FromTimestamp { get; set; }
	public virtual int? ToTimestamp { get; set; }

}

public enum Command {

	Ping,
	Error,
	SubscribeToLocationUpdates,
	LocationUpdate,
	RequestLocations,
	RequestedLocation

}