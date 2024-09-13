using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

		//app.MapGet("/", () => Results.Redirect(DatabaseExists ? "/index.html" : "/setup.html"));

		/*app.MapPost("/api/postMessage/abc", async context => {

			app.Logger.LogInformation("Query: {query}", context.Request.QueryString.Value);
			app.Logger.LogInformation("Headers: {headers}", context.Request.Headers.Select(header => $"{header.Key}: {string.Join("|", header.Value)}"));

			using StreamReader sr = new(context.Request.Body);

			app.Logger.LogInformation("Body: {body}", await sr.ReadToEndAsync());

			context.Response.StatusCode = StatusCodes.Status404NotFound;

		});*/

		/*app.MapGet("/setup.html", () => {

			if(databaseExists) {

				return Results.Redirect("/index.html");

			}

			return Results.

		});*/

		/*RewriteOptions rewriteOptions = new();

		rewriteOptions.Add(context => {

			HttpContext httpContext = context.HttpContext;

			if(httpContext.Request.Path.Value == "/setup.html" &&
				DatabaseExists) {

				httpContext.Request.Path = "/";

			}

		});

		app.UseRewriter(rewriteOptions);*/

		app.MapControllers();

		app.Run();

	}

}