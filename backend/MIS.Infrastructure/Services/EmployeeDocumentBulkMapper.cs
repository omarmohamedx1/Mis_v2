using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Services;

namespace MIS.Infrastructure.Services;

internal static class EmployeeDocumentBulkMapper
{
    internal sealed record EmployeeMatch(
        Guid Id,
        string Number,
        string? NationalId,
        string? MobileNumber,
        string Name,
        string? NameArabic,
        string? NameEnglish,
        string? Gender);

    internal sealed record ParsedFile(
        string? EmployeeNumber,
        string? NationalId,
        string? MobileNumber,
        string? NameHint,
        string? DocumentCode);

    internal sealed record StoredFile(Guid FileId, string FileName, string StorageKey, string ContentType, long Length);

    private static readonly (string Code, string[] Keywords)[] DocumentKeywords =
    [
        (RequiredEmployeeDocumentCodes.BirthCertificate, ["birthcertificate", "birth_certificate", "birth", "شهادةالميلاد", "شهادهالميلاد", "ميلاد"]),
        (RequiredEmployeeDocumentCodes.GraduationCertificate, ["graduationcertificate", "graduation_certificate", "graduation", "degree", "شهادةالتخرج", "شهادهالتخرج", "تخرج"]),
        (RequiredEmployeeDocumentCodes.NationalIdCopy, ["nationalidcopy", "national_id", "nationalid", "idcard", "id_card", "صورةالبطاقة", "صورهالبطاقه", "صورةالبطاقه", "البطاقة", "بطاقه", "بطاقة"]),
        (RequiredEmployeeDocumentCodes.MilitaryStatus, ["militarystatus", "military_status", "military", "exemption", "موقفالتجنيد", "ورقالإعفاء", "ورقالاعفاء", "تجنيد", "اعفاء", "إعفاء", "اعفا"]),
        (RequiredEmployeeDocumentCodes.CriminalRecord, ["criminalrecord", "criminal_record", "criminal", "fish", "الفيشالجنائي", "الفيش", "فيش"]),
        (RequiredEmployeeDocumentCodes.EmploymentAppointmentPaper, ["appointmentpaper", "employmentpaper", "employment_paper", "appointment", "ورقةالتعيين", "ورقهالتعيين", "التعيين", "تعيين"]),
        (RequiredEmployeeDocumentCodes.LaborOfficeRegistration, ["kaabelamal", "kaab_el_amal", "kaab", "laborofficeregistration", "labor", "كعبالعمل", "كعب"])
    ];

    private static readonly Dictionary<string, (string En, string Ar)> DocumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [RequiredEmployeeDocumentCodes.BirthCertificate] = ("Birth Certificate", "شهادة الميلاد"),
        [RequiredEmployeeDocumentCodes.GraduationCertificate] = ("Graduation Certificate", "شهادة التخرج"),
        [RequiredEmployeeDocumentCodes.NationalIdCopy] = ("National ID Copy", "صورة البطاقة"),
        [RequiredEmployeeDocumentCodes.MilitaryStatus] = ("Military Exemption / Status", "ورق الإعفاء / موقف التجنيد"),
        [RequiredEmployeeDocumentCodes.CriminalRecord] = ("Criminal Record", "الفيش الجنائي"),
        [RequiredEmployeeDocumentCodes.EmploymentAppointmentPaper] = ("Employment / Appointment Paper", "ورقة التعيين"),
        [RequiredEmployeeDocumentCodes.LaborOfficeRegistration] = ("Labor Office Registration (Kaab El Amal)", "كعب العمل")
    };

    internal static string DocumentDisplayName(string? code, bool arabic)
    {
        if (string.IsNullOrWhiteSpace(code) || !DocumentNames.TryGetValue(code, out var names)) return "";
        return arabic ? names.Ar : names.En;
    }

    internal static ParsedFile ParseFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
        if (stem.Length == 0) return new ParsedFile(null, null, null, null, null);

        var nationalIdMatch = Regex.Match(stem, @"(?<!\d)\d{14}(?!\d)").Value;
        var nationalId = nationalIdMatch.Length == 0 ? null : nationalIdMatch;

        var mobileMatch = Regex.Match(stem, @"(?<!\d)0?1[0125]\d{8}(?!\d)");
        string? mobile = mobileMatch.Success ? NormalizeMobileHint(mobileMatch.Value) : null;

        var documentCode = DetectDocumentCode(stem);
        var tokens = Regex.Split(stem, @"[_\-\s\.]+", RegexOptions.CultureInvariant)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();

        string? employeeNumber = null;
        var nameParts = new List<string>();
        foreach (var token in tokens)
        {
            if (nationalId is not null && token == nationalId) continue;
            if (mobile is not null && NormalizeMobileHint(token) == mobile) continue;
            if (IsDocumentKeywordToken(token)) continue;
            if (employeeNumber is null && LooksLikeEmployeeNumber(token))
            {
                employeeNumber = token;
                continue;
            }
            nameParts.Add(token);
        }

        var nameHint = nameParts.Count == 0 ? null : string.Join(' ', nameParts);
        return new ParsedFile(employeeNumber, nationalId, mobile, nameHint, documentCode);
    }

    internal static EmployeeDocumentBulkItem MapItem(
        StoredFile file,
        ParsedFile parsed,
        IReadOnlyCollection<EmployeeMatch> employees,
        HashSet<(Guid EmployeeId, string DocumentCode)> existingDocuments,
        HashSet<(Guid EmployeeId, string DocumentCode)> batchKeys,
        Guid? overrideEmployeeId,
        string? overrideDocumentCode,
        string duplicateAction,
        bool arabic)
    {
        var errors = new List<string>();
        var action = string.Equals(duplicateAction, "Replace", StringComparison.OrdinalIgnoreCase) ? "Replace" : "Skip";

        EmployeeMatch? employee;
        if (overrideEmployeeId.HasValue)
        {
            employee = employees.FirstOrDefault(item => item.Id == overrideEmployeeId.Value);
            if (employee is null) errors.Add("Employee Not Found / الموظف غير موجود");
        }
        else
        {
            employee = MatchEmployee(parsed, employees, errors);
        }

        string? documentCode = !string.IsNullOrWhiteSpace(overrideDocumentCode)
            ? NormalizeCode(overrideDocumentCode)
            : parsed.DocumentCode;

        if (string.IsNullOrWhiteSpace(documentCode) || !RequiredEmployeeDocumentCodes.All.Contains(documentCode))
        {
            documentCode = null;
            if (!errors.Any(message => message.Contains("Document Type", StringComparison.OrdinalIgnoreCase) || message.Contains("نوع المستند")))
                errors.Add("Document Type Not Recognized / نوع المستند غير معروف");
        }

        string status;
        if (errors.Any(message => message.Contains("Ambiguous", StringComparison.OrdinalIgnoreCase) || message.Contains("أكثر من موظف")))
            status = "AmbiguousEmployee";
        else if (employee is null)
            status = "EmployeeNotFound";
        else if (documentCode is null)
            status = "DocumentTypeNotRecognized";
        else
            status = "Ready";

        if (employee is not null && documentCode == RequiredEmployeeDocumentCodes.MilitaryStatus &&
            !EmployeePersonnelFileRules.IsMilitaryDocumentRequired(employee.Gender))
        {
            status = "Error";
            errors.Add("A military-status document is required only for male employees. / مستند التجنيد مطلوب للذكور فقط");
        }

        if (employee is not null && documentCode is not null && status == "Ready")
        {
            var key = (employee.Id, documentCode);
            if (existingDocuments.Contains(key) || batchKeys.Contains(key))
            {
                status = "DocumentAlreadyExists";
                errors.Add("Document Already Exists / المستند موجود بالفعل");
            }
            else
            {
                batchKeys.Add(key);
            }
        }

        return new EmployeeDocumentBulkItem(
            file.FileId,
            file.FileName,
            file.Length,
            file.ContentType,
            employee?.Id,
            employee?.Number,
            employee is null ? null : (arabic ? employee.NameArabic ?? employee.Name : employee.NameEnglish ?? employee.Name),
            documentCode,
            DocumentDisplayName(documentCode, arabic),
            status,
            action,
            errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static EmployeeMatch? MatchEmployee(ParsedFile parsed, IReadOnlyCollection<EmployeeMatch> employees, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(parsed.EmployeeNumber))
        {
            var matches = employees.Where(e => string.Equals(e.Number, parsed.EmployeeNumber, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Ambiguous Employee / أكثر من موظف مطابق"); return null; }
            errors.Add("Employee Not Found / الموظف غير موجود");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.NationalId))
        {
            var matches = employees.Where(e => e.NationalId == parsed.NationalId).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Ambiguous Employee / أكثر من موظف مطابق"); return null; }
            errors.Add("Employee Not Found / الموظف غير موجود");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.MobileNumber))
        {
            var matches = employees.Where(e => !string.IsNullOrWhiteSpace(e.MobileNumber) && MobilesEqual(e.MobileNumber!, parsed.MobileNumber)).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Ambiguous Employee / أكثر من موظف مطابق"); return null; }
            errors.Add("Employee Not Found / الموظف غير موجود");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.NameHint))
        {
            var matches = employees.Where(e => NameMatches(e, parsed.NameHint)).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Ambiguous Employee / أكثر من موظف مطابق"); return null; }
            errors.Add("Employee Not Found / الموظف غير موجود");
            return null;
        }

        errors.Add("Employee Not Found / الموظف غير موجود");
        return null;
    }

    private static bool NameMatches(EmployeeMatch employee, string name)
    {
        var needle = NormalizeName(name);
        if (needle.Length == 0) return false;
        return NormalizeName(employee.Name) == needle ||
               (!string.IsNullOrWhiteSpace(employee.NameArabic) && NormalizeName(employee.NameArabic) == needle) ||
               (!string.IsNullOrWhiteSpace(employee.NameEnglish) && NormalizeName(employee.NameEnglish) == needle) ||
               NameTokenMatch(employee.Name, needle) ||
               NameTokenMatch(employee.NameArabic, needle) ||
               NameTokenMatch(employee.NameEnglish, needle);
    }

    private static bool NameTokenMatch(string? haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack)) return false;
        var tokens = NormalizeName(haystack).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needles = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return needles.Length > 0 && needles.All(part => tokens.Contains(part));
    }

    private static string NormalizeName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == ' ') builder.Append(ch);
            else if (ch is '_' or '-' or '.') builder.Append(' ');
        }
        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static bool MobilesEqual(string left, string right)
    {
        static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
        var a = Digits(left);
        var b = Digits(right);
        if (a.Length >= 10) a = a[^10..];
        if (b.Length >= 10) b = b[^10..];
        return a.Length > 0 && a == b;
    }

    private static string? DetectDocumentCode(string stem)
    {
        var compact = Compact(stem);
        foreach (var (code, keywords) in DocumentKeywords)
        {
            if (keywords.Any(keyword => compact.Contains(Compact(keyword), StringComparison.Ordinal)))
                return code;
        }
        return null;
    }

    private static bool IsDocumentKeywordToken(string token)
    {
        var compact = Compact(token);
        return DocumentKeywords.SelectMany(item => item.Keywords).Any(keyword =>
        {
            var key = Compact(keyword);
            return compact.Length > 0 && (compact == key || key.Contains(compact) || compact.Contains(key));
        });
    }

    private static bool LooksLikeEmployeeNumber(string token)
    {
        if (token.Length is < 1 or > 32) return false;
        if (Regex.IsMatch(token, @"^\d{14}$")) return false;
        if (Regex.IsMatch(token, @"^0?1[0125]\d{8}$")) return false;
        return Regex.IsMatch(token, @"^[A-Za-z0-9]+$") && token.Any(char.IsDigit);
    }

    private static string Compact(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch) || ch is '_' or '-' or '.' or '/' or '\\') continue;
            builder.Append(char.ToLower(ch, CultureInfo.InvariantCulture));
        }
        return builder.ToString()
            .Replace("ة", "ه", StringComparison.Ordinal)
            .Replace("ى", "ي", StringComparison.Ordinal)
            .Replace("أ", "ا", StringComparison.Ordinal)
            .Replace("إ", "ا", StringComparison.Ordinal)
            .Replace("آ", "ا", StringComparison.Ordinal)
            .Replace("ؤ", "و", StringComparison.Ordinal)
            .Replace("ئ", "ي", StringComparison.Ordinal);
    }

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant().Replace('-', '_');

    private static string NormalizeMobileHint(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("20", StringComparison.Ordinal) && digits.Length >= 12) digits = digits[2..];
        if (digits.Length == 10 && digits.StartsWith('1')) digits = "0" + digits;
        return digits;
    }
}
