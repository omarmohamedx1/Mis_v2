using MIS.API.Configuration;
using MIS.API.Middleware;
using MIS.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// The Windows EventLog provider can be registered by the default host even when the
// process identity cannot write to the Event Log. A logging failure must never mask
// the original API exception, so keep the configured cross-platform providers only.
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddApiServices(builder.Configuration);

builder.Services.AddHostedService<MIS.API.Configuration.HrMissionNotificationWorker>();

var app = builder.Build();

// Every response uses the language requested by the web client (Accept-Language: ar|en).
// Keep this before error handling/authentication so validation, 401, 403, and business
// errors all use the same request culture.
app.UseRequestLocalization();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Vary", "Accept-Language");
    await next();
});
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CollectionOrganizationTypeMiddleware>();
app.UseMiddleware<CollectionsClassificationMiddleware>();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

// Make the development API address friendly when opened directly in a browser.
// The frontend remains the owner of the login UI.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("http://localhost:5173/login"))
        .AllowAnonymous();
}

if (app.Environment.IsDevelopment())
{
    await ApplicationDbSeeder.SeedDevelopmentDataAsync(app.Services, app.Configuration);
}

app.Run();

public partial class Program;
