using System.Text;

namespace MIS.Domain.Hr;

public static class HrOrganizationLookup
{
    private static readonly HashSet<string> GenericTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "bank", "banks", "egypt", "company", "co", "the", "mnt",
        "بنك", "البنك", "البنوك", "مصر", "ايجيبت", "إيجيبت", "شركة", "الشركة"
    };

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALEXBANK"] = ["alexbank", "alex bank", "alexandria bank", "بنك الاسكندرية", "بنك الإسكندرية"],
        ["ATTIJARIWAFA"] = ["attijariwafa", "attijariwafa egypt", "attijariwafa bank egypt", "attijari", "التجاري وفا", "التجاري وفا بنك", "التجاري وفا بنك ايجيبت"],
        ["RAYA"] = ["raya", "راية"],
        ["MNT_HALAN"] = ["halan", "mnt halan", "mnt-halan", "حالا", "ام ان تي حالا"],
        ["PREMIUM_CARD"] = ["premium card", "premiumcard", "بريميوم كارد", "بريميوم"],
        ["CAE"] = ["credit agricole", "credit agricole egypt", "كريدي اجريكول", "كريدي أجريكول"],
        ["BDC"] = ["banque du caire", "cairo bank", "بنك القاهرة"],
        ["AMAN"] = ["aman", "أمان"],
        ["HSBC"] = ["hsbc", "hsbc egypt", "hsbc bank", "hsbc مصر", "اتش اس بي سي", "اتش إس بي سي", "اتش اس بى سى"]
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Normalize(NormalizationForm.FormKC)
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('&', ' ')
            .Replace("manegar", "manager", StringComparison.OrdinalIgnoreCase);
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    public static bool Matches(string? value, string code, string? nameEnglish, string? nameArabic)
    {
        var needle = Normalize(value);
        if (needle.Length == 0) return false;
        if (EqualsNorm(needle, code) || EqualsNorm(needle, nameEnglish) || EqualsNorm(needle, nameArabic))
            return true;
        if (Aliases.TryGetValue(code, out var aliases) && aliases.Any(alias => EqualsNorm(needle, alias)))
            return true;

        var needleTokens = Tokens(needle).Where(token => token.Length > 1 && !GenericTokens.Contains(token)).ToArray();
        if (needleTokens.Length == 0) return false;
        var haystack = Tokens($"{code} {nameEnglish} {nameArabic} {(Aliases.TryGetValue(code, out var extra) ? string.Join(' ', extra) : string.Empty)}");
        return needleTokens.All(haystack.Contains);
    }

    public static bool Mentions(string? value, string code, string? nameEnglish, string? nameArabic)
    {
        if (Matches(value, code, nameEnglish, nameArabic)) return true;
        var haystack = Normalize(value);
        if (haystack.Length == 0) return false;
        return DistinctMarkers(code, nameEnglish, nameArabic)
            .Any(marker => haystack.Contains(marker, StringComparison.Ordinal));
    }

    private static bool EqualsNorm(string needle, string? candidate)
    {
        var normalized = Normalize(candidate);
        return normalized.Length > 0 && string.Equals(needle, normalized, StringComparison.Ordinal);
    }

    private static HashSet<string> Tokens(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> DistinctMarkers(string code, string? nameEnglish, string? nameArabic)
    {
        var markers = new HashSet<string>(StringComparer.Ordinal);
        AddMarkers(markers, code);
        AddMarkers(markers, nameEnglish);
        AddMarkers(markers, nameArabic);
        if (Aliases.TryGetValue(code, out var aliases))
        {
            foreach (var alias in aliases)
                AddMarkers(markers, alias);
        }

        return markers;
    }

    private static void AddMarkers(HashSet<string> markers, string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length >= 3 && !GenericTokens.Contains(normalized))
            markers.Add(normalized);
        foreach (var token in Tokens(normalized))
        {
            if (token.Length >= 3 && !GenericTokens.Contains(token))
                markers.Add(token);
        }
    }
}
