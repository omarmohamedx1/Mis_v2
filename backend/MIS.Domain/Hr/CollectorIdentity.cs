using System.Globalization;
using System.Text;

namespace MIS.Domain.Hr;

public sealed record CollectorIdentityCandidate(
    Guid UserId,
    string FullName,
    string Username,
    string Email,
    string? EmployeeNumber,
    string? EmployeeFullName,
    string? EmployeeFullNameArabic,
    string? EmployeeFullNameEnglish);

public enum CollectorMatchKind
{
    None,
    Unique,
    Ambiguous
}

public readonly record struct CollectorMatch(CollectorMatchKind Kind, Guid? UserId);

public static class CollectorIdentity
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim().Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var mapped = ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' or 'ئ' => 'ي',
                'ؤ' => 'و',
                'ة' => 'ه',
                _ => ch
            };
            if (char.IsWhiteSpace(mapped) || mapped is '-' or '_' or '.' or '/' or '\\')
            {
                if (builder.Length > 0 && builder[^1] != ' ') builder.Append(' ');
                continue;
            }
            builder.Append(char.ToLower(mapped, CultureInfo.InvariantCulture));
        }
        return builder.ToString().Trim();
    }

    public static CollectorMatch Match(IReadOnlyCollection<CollectorIdentityCandidate> directory, params string?[] values)
    {
        CollectorMatch? unique = null;
        var ambiguous = false;
        foreach (var value in ExpandNeedles(values))
        {
            var result = MatchSingle(directory, value);
            if (result.Kind == CollectorMatchKind.Unique)
            {
                if (unique is { } found && found.UserId != result.UserId)
                    return new(CollectorMatchKind.Ambiguous, null);
                unique = result;
            }
            else if (result.Kind == CollectorMatchKind.Ambiguous)
            {
                ambiguous = true;
            }
        }
        if (unique is { } match) return match;
        return ambiguous ? new(CollectorMatchKind.Ambiguous, null) : new(CollectorMatchKind.None, null);
    }

    private static IEnumerable<string> ExpandNeedles(string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            yield return value;
            var (arabic, english) = EmployeeName.SplitScripts(value);
            if (!string.IsNullOrWhiteSpace(arabic) && !string.Equals(arabic, value.Trim(), StringComparison.Ordinal))
                yield return arabic;
            if (!string.IsNullOrWhiteSpace(english) && !string.Equals(english, value.Trim(), StringComparison.Ordinal))
                yield return english;
        }
    }

    public static Guid? UniqueMatch(IReadOnlyCollection<CollectorIdentityCandidate> directory, params string?[] values)
    {
        var match = Match(directory, values);
        return match.Kind == CollectorMatchKind.Unique ? match.UserId : null;
    }

    private static CollectorMatch MatchSingle(IReadOnlyCollection<CollectorIdentityCandidate> directory, string? value)
    {
        var needle = Normalize(value);
        if (needle.Length == 0) return new(CollectorMatchKind.None, null);

        var identifiers = Distinct(directory.Where(x =>
            EqualsNorm(x.EmployeeNumber, needle) || EqualsNorm(x.Username, needle) || EqualsNorm(x.Email, needle)));
        if (identifiers.Count == 1) return new(CollectorMatchKind.Unique, identifiers[0].UserId);
        if (identifiers.Count > 1) return new(CollectorMatchKind.Ambiguous, null);

        var names = Distinct(directory.Where(x => Names(x).Any(name => EqualsNorm(name, needle))));
        var overlap = Distinct(directory.Where(x => OverlapsName(x, needle)));
        if (names.Count == 1) return overlap.Count > 1 ? new(CollectorMatchKind.Ambiguous, null) : new(CollectorMatchKind.Unique, names[0].UserId);
        if (names.Count > 1) return new(CollectorMatchKind.Ambiguous, null);
        if (overlap.Count == 1) return new(CollectorMatchKind.Unique, overlap[0].UserId);
        if (overlap.Count > 1) return new(CollectorMatchKind.Ambiguous, null);
        return new(CollectorMatchKind.None, null);
    }

    private static bool EqualsNorm(string? candidate, string needle)
    {
        var normalized = Normalize(candidate);
        return normalized.Length > 0 && string.Equals(normalized, needle, StringComparison.Ordinal);
    }

    private static bool OverlapsName(CollectorIdentityCandidate candidate, string needle)
    {
        var fileTokens = Tokens(needle);
        if (fileTokens.Length < 2) return false;
        return Names(candidate).Any(name =>
        {
            var personTokens = Tokens(name);
            if (personTokens.Length < 2) return false;
            var shorter = fileTokens.Length <= personTokens.Length ? fileTokens : personTokens;
            var longer = fileTokens.Length <= personTokens.Length ? personTokens : fileTokens;
            return shorter.All(longer.Contains);
        });
    }

    private static string[] Names(CollectorIdentityCandidate candidate)
    {
        return new[]
            {
                candidate.FullName,
                candidate.EmployeeFullName,
                candidate.EmployeeFullNameArabic,
                candidate.EmployeeFullNameEnglish
            }
            .SelectMany(name =>
            {
                var (arabic, english) = EmployeeName.SplitScripts(name);
                return new[] { name, arabic, english };
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] Tokens(string value) =>
        Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<CollectorIdentityCandidate> Distinct(IEnumerable<CollectorIdentityCandidate> source) =>
        source.GroupBy(x => x.UserId).Select(x => x.First()).ToArray();
}
