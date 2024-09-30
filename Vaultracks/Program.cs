using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SQLite;

namespace Vaultracks;

public class Program {

	public static void Main(string[] args) {

		WebApplicationBuilder builder =
			WebApplication.CreateSlimBuilder(args);

		builder.Services.AddControllers();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services.AddHttpLogging(options => {

			options.LoggingFields |= HttpLoggingFields.ResponseBody;

		});

		WebApplication app = builder.Build();

		if(app.Environment.IsDevelopment()) {

			app.UseSwagger();
			app.UseSwaggerUI();

		}

		app.UseStaticFiles();
		app.UseHttpLogging();
		app.UseWebSockets();

		app.MapGet("/", () => Results.Redirect("/index.html"));

		app.MapControllers();

		app.Lifetime
			.ApplicationStopping
			.Register(async () => {

				app.Logger.LogInformation("Closing db connections...");

				foreach((UserAuth userAuth, SQLiteAsyncConnection Db) in DatabaseManager.ActiveConnections) {

					await Db.CloseAsync();

					app.Logger.LogInformation("Closing db for {username}", userAuth.Username);

				}

			});

		app.Logger
			.LogInformation("Enumerating databases: {dbNames}",
							string.Join(", ", DatabaseManager.ListDbs()));

		app.Run();

	}

}