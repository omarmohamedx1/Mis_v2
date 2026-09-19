namespace MIS.Domain.Hr;

public static class EmployeeName
{
    public static bool ContainsArabicLetters(string value) => value.Any(IsArabicLetter);

    public static bool IsArabicLetter(char character) =>
        character is (>= '\u0600' and <= '\u06FF') or (>= '\u0750' and <= '\u077F')
            or (>= '\u08A0' and <= '\u08FF') or (>= '\uFB50' and <= '\uFDFF') or (>= '\uFE70' and <= '\uFEFF');

    public static bool IsLatinLetter(char character) =>
        character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    public static bool IsArabicName(string? value) => MatchesScript(value, arabic: true);

    public static bool IsEnglishName(string? value) => MatchesScript(value, arabic: false);

    public static string? RequireArabic(string? value) => Require(value, arabic: true,
        "Arabic full name must contain Arabic letters only.");

    public static string? RequireEnglish(string? value) => Require(value, arabic: false,
        "English full name must contain English letters only.");

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool MatchesScript(string? value, bool arabic)
    {
        var normalized = Normalize(value);
        if (normalized is null) return true;
        var hasLetter = false;
        foreach (var character in normalized)
        {
            if (arabic ? IsArabicLetter(character) : IsLatinLetter(character))
            {
                hasLetter = true;
                continue;
            }
            if (IsNameMark(character, arabic)) continue;
            return false;
        }
        return hasLetter;
    }

    private static bool IsNameMark(char character, bool arabic) =>
        char.IsWhiteSpace(character) || character is '-' or '\'' or '\u2019' or '.'
        || (arabic && character is '،' or 'ـ');

    private static string? Require(string? value, bool arabic, string message)
    {
        var normalized = Normalize(value);
        if (normalized is null) return null;
        if (!(arabic ? IsArabicName(normalized) : IsEnglishName(normalized)))
            throw new ArgumentException(message);
        return normalized;
    }

    public static (string Canonical, string? Arabic, string? English) Resolve(
        string? fullName, string? arabic, string? english)
    {
        var fromFull = SplitScripts(fullName);
        var fromArabic = SplitScripts(arabic);
        var fromEnglish = SplitScripts(english);
        var ar = fromArabic.Arabic ?? fromFull.Arabic ?? fromEnglish.Arabic;
        var en = fromEnglish.English ?? fromFull.English ?? fromArabic.English;
        var canonical = Normalize(fullName) ?? Normalize(arabic) ?? Normalize(english) ?? en ?? ar ?? string.Empty;
        if (canonical.Length > 0 && ar is null && en is null)
        {
            if (ContainsArabicLetters(canonical)) ar = canonical;
            else en = canonical;
        }
        return (canonical, ar, en);
    }

    public static (string? Arabic, string? English) SplitScripts(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null) return (null, null);
        var arabic = new List<string>();
        var english = new List<string>();
        foreach (var token in normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var arabicPart = new string(token.Where(character => IsArabicLetter(character) || character is 'ـ').ToArray());
            var englishPart = new string(token.Where(IsLatinLetter).ToArray());
            if (arabicPart.Length > 0) arabic.Add(arabicPart);
            if (englishPart.Length > 0) english.Add(englishPart);
        }
        return (
            arabic.Count > 0 ? string.Join(' ', arabic) : null,
            english.Count > 0 ? string.Join(' ', english) : null);
    }

    public static (string Canonical, string? Arabic, string? English) FillMissing(
        string? fullName, string? arabic, string? english) =>
        Resolve(fullName, arabic, english);

    public static string Display(bool isArabic, string fullName, string? arabic, string? english)
    {
        var resolved = FillMissing(fullName, arabic, english);
        return isArabic
            ? resolved.Arabic ?? resolved.Canonical
            : resolved.English ?? resolved.Canonical;
    }
}
