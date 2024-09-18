using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
		builder.Services.AddHttpLogging(options => { });

		WebApplication app = builder.Build();

		if(app.Environment.IsDevelopment()) {

			app.UseSwagger();
			app.UseSwaggerUI();

		}

		app.UseStaticFiles();
		app.UseHttpLogging();

		app.MapGet("/", () => Results.Redirect("/index.html"));

		app.MapControllers();

		app.Lifetime
			.ApplicationStopping
			.Register(async () => {

				app.Logger.LogInformation("Closing db connections...");

				foreach((DBAccess dbAccess, SQLiteAsyncConnection Db) in ApiController.ActiveDatabaseConnections) {

					await Db.CloseAsync();

					app.Logger.LogInformation("Closing db for {username}", dbAccess.Username);

				}

			});

		app.Run();

	}

}