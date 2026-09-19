using System.Text.Json;
using System.Text.Json.Nodes;

namespace MIS.Domain.Services;

public static class CollectionSettingsPolicy
{
    public const string GraceDaysKey = "ptpGraceDays";
    public const string ToleranceAmountKey = "ptpToleranceAmount";
    public const int MaxGraceDays = 30;

    public static (int GraceDays, decimal ToleranceAmount)? Parse(string? json)
    {
        var policy = Read(json);
        if (policy.GraceDays is null && policy.ToleranceAmount is null) return null;
        return (policy.GraceDays ?? 0, policy.ToleranceAmount ?? 0);
    }

    public static (int? GraceDays, decimal? ToleranceAmount) Read(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = document.RootElement;
            int? days = root.TryGetProperty(GraceDaysKey, out var graceValue) && graceValue.TryGetInt32(out var parsedDays)
                ? Math.Clamp(parsedDays, 0, MaxGraceDays)
                : null;
            decimal? amount = root.TryGetProperty(ToleranceAmountKey, out var toleranceValue) && toleranceValue.TryGetDecimal(out var parsedAmount)
                ? Math.Max(0, parsedAmount)
                : null;
            return (days, amount);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    public static string Merge(string? existingJson, int? graceDays, decimal? toleranceAmount)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(existingJson) ? "{}" : existingJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        if (graceDays.HasValue) root[GraceDaysKey] = Math.Clamp(graceDays.Value, 0, MaxGraceDays);
        else root.Remove(GraceDaysKey);

        if (toleranceAmount.HasValue) root[ToleranceAmountKey] = Math.Max(0, toleranceAmount.Value);
        else root.Remove(ToleranceAmountKey);

        return root.ToJsonString();
    }
}
