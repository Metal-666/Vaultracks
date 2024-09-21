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
using System.Threading;
using System.Threading.Tasks;

using Timer = System.Timers.Timer;

namespace Vaultracks;

public class WebSocketController : ControllerBase {

	protected static readonly JsonSerializerOptions JsonSerializerOptions =
		new() {

			PropertyNamingPolicy = JsonNamingPolicy.CamelCase

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

	[Route("/ws")]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task Ws([FromQuery] string username, [FromQuery] string databaseKey) {

		UserAuth? userAuth =
			UserAuth.ParseUrlEncoded(username, databaseKey);

		if(userAuth == null) {

			Response.StatusCode = StatusCodes.Status400BadRequest;
			await Response.WriteAsync("Failed to parse auth data!");

			return;

		}

		if(!HttpContext.WebSockets.IsWebSocketRequest) {

			Response.StatusCode = StatusCodes.Status400BadRequest;
			await Response.WriteAsync("Request is not a websocket request!");

			return;

		}

		using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

		WebSocketConnection connection = new() {

			WebSocket = webSocket,
			UserAuth = userAuth

		};

		Connections.Add(connection);

		Logger.LogInformation("Established WS connection with client!");

		CancellationTokenSource cancellationTokenSource = new();

		while(true) {

			Timer timeout = new(TimeSpan.FromSeconds(20));

			timeout.Elapsed +=
				(sender, args) =>
					cancellationTokenSource.Cancel();

			timeout.Start();

			byte[] buffer = new byte[1024 * 4];

			WebSocketReceiveResult result =
				await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
												cancellationTokenSource.Token);

			timeout.Dispose();

			if(cancellationTokenSource.IsCancellationRequested) {

				await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
											"Connection timeout!",
											CancellationToken.None);

				return;

			}

			if(result.CloseStatus.HasValue) {

				await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
											null,
											CancellationToken.None);

				return;

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

					EventBus.LocationEvents
							.Where(locationEvent =>
												locationEvent.UserAuth == userAuth)
							.Select(locationEvent =>
												locationEvent.Location)
							.Subscribe(async location => {

								await SendMessage(connection.WebSocket,
													new() {

														Command = Command.LocationUpdate,
														LocationJson = JsonSerializer.Serialize(location)

													});

							});

					SQLiteAsyncConnection? db = await DatabaseManager.GetDb(userAuth, true);

					if(db == null) {

						await SendError(connection.WebSocket,
										"Failed to initialize the database!");

						break;

					}

					Location? location =
						await db.Table<Location>()
								.OrderByDescending(location =>
																location.CreatedAt)
								.FirstOrDefaultAsync();

					if(location == null) {

						await SendError(connection.WebSocket,
										"Failed to retrieve location!");

						break;

					}

					await SendMessage(connection.WebSocket,
										new() {

											Command = Command.LocationUpdate,
											LocationJson = JsonSerializer.Serialize(location)

										});

					break;

				}

			}

		}

	}

	public virtual async Task ReceivedLocationUpdate(UserAuth userAuth, Location location) {

		WebSocketConnection? connection =
			Connections.Where(connection =>
										connection.ReceiveLocationUpdates)
						.FirstOrDefault(connection =>
													connection.UserAuth == userAuth);

		if(connection == null) {

			return;

		}

		await SendMessage(connection.WebSocket,
							new() {

								Command = Command.LocationUpdate,
								LocationJson = JsonSerializer.Serialize(location)

							});

	}

	public virtual async Task SendMessage(WebSocket webSocket, WebSocketMessage message) {

		string jsonData = JsonSerializer.Serialize(message, JsonSerializerOptions);

		Logger.LogWarning("Sending message: {jsonData}", jsonData);

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

}

public enum Command {

	Ping,
	Error,
	SubscribeToLocationUpdates,
	LocationUpdate

}