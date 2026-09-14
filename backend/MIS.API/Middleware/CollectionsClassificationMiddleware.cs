using MIS.Application.Interfaces;

namespace MIS.API.Middleware;

/// <summary>
/// Reads the portfolio classification the caller scoped the request to, from either the
/// <c>primaryClassification</c>/<c>subClassification</c> query string or the
/// <c>X-Collections-Primary</c>/<c>X-Collections-Sub</c> headers. Requests that supply neither stay
/// unscoped so legacy and global collections pages keep returning every portfolio.
/// </summary>
public sealed class CollectionsClassificationMiddleware(RequestDelegate next)
{
    private const string PrimaryQueryKey = "primaryClassification";
    private const string SubQueryKey = "subClassification";
    private const string PrimaryHeader = "X-Collections-Primary";
    private const string SubHeader = "X-Collections-Sub";

    public async Task InvokeAsync(HttpContext context, ICollectionsClassificationContext classification)
    {
        classification.Set(
            Read(context, PrimaryQueryKey, PrimaryHeader),
            Read(context, SubQueryKey, SubHeader));
        classification.RequireValid();
        await next(context);
    }

    private static string? Read(HttpContext context, string queryKey, string headerName)
    {
        var value = context.Request.Query[queryKey].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)) value = context.Request.Headers[headerName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
