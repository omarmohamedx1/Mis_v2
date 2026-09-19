using System.Globalization;
using System.Text;

namespace MIS.Application.Common;

public readonly record struct ParsedEgyptianNationalId(string NationalId, DateOnly DateOfBirth, string Gender);

public static class EgyptianHrDataValidator
{
    public static bool TryParseNationalId(string? value, out ParsedEgyptianNationalId parsed, out string error)
    {
        parsed = default;
        var normalized = NormalizeDigits(value, allowLeadingPlus: false);
        if (normalized is null || normalized.Length != 14 || normalized.Any(character => !char.IsAsciiDigit(character)))
        {
            error = "Egyptian national ID must contain exactly 14 digits.";
            return false;
        }

        var century = normalized[0] switch
        {
            '2' => 1900,
            '3' => 2000,
            _ => 0
        };
        if (century == 0)
        {
            error = "Egyptian national ID has an invalid century digit.";
            return false;
        }

        if (!int.TryParse(normalized.AsSpan(1, 2), out var year) ||
            !int.TryParse(normalized.AsSpan(3, 2), out var month) ||
            !int.TryParse(normalized.AsSpan(5, 2), out var day))
        {
            error = "Egyptian national ID contains an invalid birth date.";
            return false;
        }

        DateOnly encodedBirthDate;
        try { encodedBirthDate = new DateOnly(century + year, month, day); }
        catch (ArgumentOutOfRangeException)
        {
            error = "Egyptian national ID contains an invalid birth date.";
            return false;
        }

        if (encodedBirthDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            error = "Egyptian national ID cannot contain a future birth date.";
            return false;
        }

        parsed = new ParsedEgyptianNationalId(normalized, encodedBirthDate, (normalized[12] - '0') % 2 == 0 ? "female" : "male");
        error = string.Empty;
        return true;
    }

    public static string? NormalizeNationalId(string? value, DateOnly? dateOfBirth, string? gender)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!TryParseNationalId(value, out var parsed, out var error))
            throw new HrValidationException(error);
        if (dateOfBirth.HasValue && dateOfBirth.Value != parsed.DateOfBirth)
            throw new HrValidationException("Date of birth does not match the Egyptian national ID.");

        var normalizedGender = gender?.Trim().ToLowerInvariant();
        if (normalizedGender is "male" or "female" && normalizedGender != parsed.Gender)
            throw new HrValidationException("Gender does not match the Egyptian national ID.");

        return parsed.NationalId;
    }

    public static string? NormalizePhone(string? value, string fieldName, bool required = false)
    {
        var normalized = NormalizeDigits(value, allowLeadingPlus: true);
        if (normalized is null)
        {
            if (required) throw new HrValidationException("Phone number is required.");
            return null;
        }

        if (normalized.StartsWith("0020", StringComparison.Ordinal)) normalized = "+20" + normalized[4..];
        if (normalized.StartsWith("+20", StringComparison.Ordinal))
        {
            var local = normalized[3..].TrimStart('0');
            normalized = "0" + local;
        }

        if (normalized.StartsWith('+'))
        {
            if (normalized.Length is < 9 or > 16 || normalized[1..].Any(character => !char.IsAsciiDigit(character)))
                throw new HrValidationException("Phone number must be a valid international phone number.");
            return normalized;
        }

        if (normalized.Length != 11 ||
            !normalized.StartsWith("01", StringComparison.Ordinal) ||
            normalized[2] is not ('0' or '1' or '2' or '5') ||
            normalized.Any(character => !char.IsAsciiDigit(character)))
            throw new HrValidationException("Phone number must be a valid Egyptian mobile number (010, 011, 012, or 015).");

        return normalized;
    }

    public static string? NormalizeIban(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length is < 15 or > 34 ||
            normalized.Length < 4 ||
            !char.IsAsciiLetter(normalized[0]) || !char.IsAsciiLetter(normalized[1]) ||
            !char.IsAsciiDigit(normalized[2]) || !char.IsAsciiDigit(normalized[3]))
            throw new HrValidationException("IBAN format is invalid.");
        if (normalized.StartsWith("EG", StringComparison.Ordinal) &&
            (normalized.Length != 29 || normalized[2..].Any(character => !char.IsAsciiDigit(character))))
            throw new HrValidationException("Egyptian IBAN must start with EG and contain 29 characters.");

        var rearranged = normalized[4..] + normalized[..4];
        var remainder = 0;
        foreach (var character in rearranged)
        {
            if (char.IsAsciiDigit(character))
            {
                remainder = ((remainder * 10) + (character - '0')) % 97;
                continue;
            }
            if (!char.IsAsciiLetter(character)) throw new HrValidationException("IBAN format is invalid.");
            var numericValue = character - 'A' + 10;
            remainder = ((remainder * 100) + numericValue) % 97;
        }
        if (remainder != 1) throw new HrValidationException("IBAN checksum is invalid.");
        return normalized;
    }

    private static string? NormalizeDigits(string? value, bool allowLeadingPlus)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (allowLeadingPlus && character == '+' && result.Length == 0)
            {
                result.Append(character);
                continue;
            }
            var numericValue = CharUnicodeInfo.GetDecimalDigitValue(character);
            if (numericValue >= 0)
            {
                result.Append((char)('0' + numericValue));
                continue;
            }
            if (character is ' ' or '-' or '(' or ')') continue;
            result.Append(character);
        }
        return result.ToString();
    }
}
