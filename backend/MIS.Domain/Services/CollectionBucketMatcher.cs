using System.Globalization;

namespace MIS.Domain.Services;

/// <summary>
/// Resolves a delinquency bucket for an imported collection row when the source
/// spreadsheet provides a bucket label (e.g. "Current BKT" = "BKT More") instead of,
/// or in addition to, an explicit days-past-due value.
/// </summary>
public static class CollectionBucketMatcher
{
    public sealed record BucketCandidate(int Index, string Code, string NameArabic, string NameEnglish, int? MinimumDays, int? MaximumDays, int SortOrder);

    public static string Normalize(string? value) =>
        new((value ?? string.Empty).Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>True when <paramref name="dpd"/> falls inside the bucket's day range.</summary>
    public static bool MatchesDays(int dpd, int? minimumDays, int? maximumDays) =>
        minimumDays.HasValue && dpd >= minimumDays && (!maximumDays.HasValue || dpd <= maximumDays);

    /// <summary>True when the free-text bucket label matches the bucket code or a localized name.</summary>
    public static bool MatchesName(string? bucketText, string code, string? nameArabic, string? nameEnglish)
    {
        var needle = Normalize(bucketText);
        if (needle.Length == 0) return false;
        return needle == Normalize(code) || needle == Normalize(nameArabic) || needle == Normalize(nameEnglish);
    }

    /// <summary>
    /// Picks the best bucket for a row. Priority: explicit day-range match, then exact
    /// label match, then a heuristic for "more"/"180+" severe labels, then the most
    /// delinquent bucket as a safe fallback. Returns null only when no buckets exist.
    /// </summary>
    public static BucketCandidate? Resolve(IReadOnlyList<BucketCandidate> buckets, int? daysPastDue, string? bucketText)
    {
        if (buckets.Count == 0) return null;

        if (daysPastDue is int dpd && dpd >= 0)
        {
            var byDays = buckets.FirstOrDefault(bucket => MatchesDays(dpd, bucket.MinimumDays, bucket.MaximumDays));
            if (byDays is not null) return byDays;
        }

        var normalized = Normalize(bucketText);
        if (normalized.Length > 0)
        {
            var byName = buckets.FirstOrDefault(bucket => MatchesName(bucketText, bucket.Code, bucket.NameArabic, bucket.NameEnglish));
            if (byName is not null) return byName;

            // "BKT More", "180+", "write off"/"legal" style labels => most delinquent bucket.
            if (normalized.Contains("more") || normalized.Contains("180") || normalized.Contains("plus")
                || normalized.Contains("writeoff") || normalized.Contains("legal"))
                return MostDelinquent(buckets);

            // A bare number inside the label (e.g. "BKT 90") => match that number by day range.
            var digits = new string(normalized.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var labelDays))
            {
                var byLabelDays = buckets.FirstOrDefault(bucket => MatchesDays(labelDays, bucket.MinimumDays, bucket.MaximumDays));
                if (byLabelDays is not null) return byLabelDays;
            }
        }

        return MostDelinquent(buckets);
    }

    private static BucketCandidate MostDelinquent(IReadOnlyList<BucketCandidate> buckets) =>
        buckets
            .OrderByDescending(bucket => bucket.MinimumDays ?? int.MinValue)
            .ThenByDescending(bucket => bucket.SortOrder)
            .First();
}
