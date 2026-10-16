using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MIS.API.Controllers;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class AccountingRoutingTests
{
    [Theory]
    [InlineData("accounting/book/entries", "submit", "AccountingBook", "Transition")]
    [InlineData("accounting/book/entries", "approve", "AccountingBook", "Transition")]
    [InlineData("accounting/book/entries", "post", "AccountingBook", "Transition")]
    [InlineData("accounting/book/entries", "cancel", "AccountingBook", "Transition")]
    [InlineData("accounting/book/entries", "reverse", "AccountingBook", "Transition")]
    [InlineData("accounting/transportation", "approve", "Accounting", "TransitionTransportation")]
    [InlineData("accounting/transportation", "pay", "Accounting", "TransitionTransportation")]
    [InlineData("accounting/collector-commissions", "submit", "Accounting", "TransitionCollector")]
    [InlineData("accounting/collector-commissions", "pay", "Accounting", "TransitionCollector")]
    [InlineData("accounting/supervisor-commissions", "approve", "Accounting", "TransitionSupervisor")]
    [InlineData("accounting/supervisor-commissions", "pay", "Accounting", "TransitionSupervisor")]
    [InlineData("finance/periods", "soft-close", "Finance", "ChangePeriod")]
    [InlineData("finance/periods", "close", "Finance", "ChangePeriod")]
    [InlineData("finance/periods", "reopen", "Finance", "ChangePeriod")]
    public async Task Posted_operation_matches_the_real_controller_route(string path, string operation, string controller, string action)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(AccountingBookController).Assembly.FullName });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(AccountingBookController).Assembly);
        await using var app = builder.Build();
        app.UseRouting();
        // Inspect actual MVC endpoint selection before authentication or controller execution.
        // No database, login credentials or financial mutations are involved in this test.
        app.Use(async (HttpContext context, RequestDelegate _) =>
        {
            var matched = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            context.Response.StatusCode = matched is null ? 404 : 200;
            if (matched is not null)
            {
                context.Response.Headers["X-Test-Controller"] = matched.ControllerName;
                context.Response.Headers["X-Test-Action"] = matched.ActionName;
                context.Response.Headers["X-Test-Operation"] = context.Request.RouteValues["operation"]?.ToString();
            }
            await context.Response.WriteAsync("Routing probe");
        });
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var response = await client.PostAsync($"/api/{path}/58264aea-195e-43b9-92bb-f246eec4bfd4/{operation}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(controller, Assert.Single(response.Headers.GetValues("X-Test-Controller")));
            Assert.Equal(action, Assert.Single(response.Headers.GetValues("X-Test-Action")));
            Assert.Equal(operation, Assert.Single(response.Headers.GetValues("X-Test-Operation")));
        }
        finally { await app.StopAsync(); }
    }
}
