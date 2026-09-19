using System.Globalization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Constants;
using MIS.Domain.Hr;

namespace MIS.Infrastructure.Services;

internal static class EmployeeImportMapper
{
    internal static readonly string[] Fields =
    [
        "MobileNumber", "EmployeeNumber", "FullNameArabic", "FullNameEnglish", "Gender",
        "Department", "Organization", "WorkNumber", "PackageType", "Position", "OperationalRole", "NationalId", "DateOfBirth",
        "WorkStartDate", "FingerprintEnrollmentDate", "WorkEndDate", "Address", "Status",
        "BasicSalary", "Allowances"
    ];
    internal sealed record Lookup(Guid Id, string Name, string Code, string? Arabic, Guid? DepartmentId = null);

    internal static SaveEmployeeRequest Map(Dictionary<string, string> values, string? dateFormat,
        IReadOnlyCollection<Lookup> departments, IReadOnlyCollection<Lookup> positions, List<string> errors,
        List<string>? warnings = null, IReadOnlyCollection<Lookup>? organizations = null, Lookup? collectionsDepartment = null)
    {
        string Value(string field) => values.GetValueOrDefault(field)?.Trim() ?? "";
        DateOnly? Date(string field)
        {
            var text = Value(field);
            if (text.Length == 0) return null;
            if (TryDate(NormalizeDigits(text), dateFormat, out var date)) return date;
            errors.Add($"{field}: invalid date.");
            return null;
        }
        decimal? Money(string field)
        {
            var text = NormalizeDigits(Value(field))
                .Replace(" ", string.Empty)
                .Replace("٬", string.Empty)
                .Replace("٫", ".")
                .Replace(",", string.Empty);
            if (text.Length == 0) return null;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0)
                return amount;
            errors.Add($"{field}: invalid amount.");
            return null;
        }
        static string NormalizeLookup(string? text)
        {
            var normalized = (text ?? string.Empty).Normalize()
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Replace("manegar", "manager", StringComparison.OrdinalIgnoreCase)
                .Replace("administration", "admin", StringComparison.OrdinalIgnoreCase);
            return string.Join(' ', normalized.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        static bool Matches(Lookup option, string value) =>
            new[] { option.Name, option.Code, option.Arabic, option.Id.ToString() }
                .Any(name => string.Equals(NormalizeLookup(name), NormalizeLookup(value), StringComparison.OrdinalIgnoreCase));
        static bool MatchesOrganization(Lookup option, string value) =>
            HrOrganizationLookup.Matches(value, option.Code, option.Name, option.Arabic);
        Lookup? LookupValue(string field, IReadOnlyCollection<Lookup> options, bool reportMissing = true)
        {
            var value = Value(field);
            if (value.Length == 0)
            {
                if (reportMissing) errors.Add($"{field}: must match one existing lookup value.");
                return null;
            }
            var matches = options.Where(option => Matches(option, value)).ToArray();
            if (matches.Length == 1) return matches[0];
            if (reportMissing)
                errors.Add(matches.Length == 0 && field is "Department" or "Position" or "Organization"
                    ? $"{field} not found: {value}"
                    : $"{field}: must match one existing lookup value.");
            return null;
        }
        static bool IsBankDepartmentAlias(string? value)
        {
            var normalized = NormalizeLookup(value).ToLowerInvariant();
            return normalized is "bank" or "banks" or "بنك" or "البنك" or "البنوك";
        }

        var clientOrganizations = organizations ?? [];
        var internalDepartments = departments.Where(option => !DepartmentCodes.IsLegacyTitleUnit(option.Code)).ToArray();
        Lookup? FindOrganization(string value)
        {
            var matches = clientOrganizations.Where(option => MatchesOrganization(option, value)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        var assignedOrganizations = new List<Lookup>();
        var explicitOrganizationValue = Value("Organization");
        var explicitOrganization = explicitOrganizationValue.Length == 0 ? null : FindOrganization(explicitOrganizationValue);
        if (explicitOrganizationValue.Length > 0 && explicitOrganization is null)
            errors.Add($"Organization not found: {explicitOrganizationValue}");
        if (explicitOrganization is not null) assignedOrganizations.Add(explicitOrganization);

        var position = LookupValue("Position", positions);
        var departmentSource = Value("Department");
        var departmentMatches = departmentSource.Length == 0
            ? []
            : internalDepartments.Where(option => Matches(option, departmentSource)).ToArray();
        var departmentAmbiguous = departmentMatches.Length > 1;
        if (departmentAmbiguous)
            errors.Add("Department: must match one existing lookup value.");
        var department = departmentMatches.Length == 1 ? departmentMatches[0] : null;
        if (department is not null && DepartmentCodes.IsLegacyTitleUnit(department.Code))
            department = null;
        if (!departmentAmbiguous && department is null && departmentSource.Length > 0 && position is not null && Matches(position, departmentSource) && position.DepartmentId is Guid positionDepartmentId)
        {
            department = internalDepartments.SingleOrDefault(option => option.Id == positionDepartmentId);
            if (department is not null)
                warnings?.Add("Department was inferred from the selected position.");
        }
        if (!departmentAmbiguous && department is null && departmentSource.Length > 0)
        {
            var organizationMatch = FindOrganization(departmentSource);
            if (organizationMatch is not null)
            {
                if (assignedOrganizations.All(item => item.Id != organizationMatch.Id))
                    assignedOrganizations.Add(organizationMatch);
                department = collectionsDepartment;
                warnings?.Add("A bank/company value was recorded as department; the employee was assigned to Collections.");
            }
            else if (IsBankDepartmentAlias(departmentSource))
            {
                department = collectionsDepartment;
                warnings?.Add("Bank is not an HR department. The employee was assigned to Collections; set the assigned bank/company separately.");
            }
            else
            {
                var titleMatches = positions.Where(option => Matches(option, departmentSource)).ToArray();
                var inferredCode = DepartmentCodes.InferFromTitle(departmentSource);
                if (titleMatches.Length == 1)
                {
                    position ??= titleMatches[0];
                    department = titleMatches[0].DepartmentId is Guid linkedId
                        ? internalDepartments.SingleOrDefault(option => option.Id == linkedId)
                        : internalDepartments.FirstOrDefault(option => string.Equals(option.Code, inferredCode, StringComparison.OrdinalIgnoreCase));
                    department ??= collectionsDepartment;
                    warnings?.Add("A job title was recorded as department; the internal department was inferred from the title.");
                }
                else if (inferredCode is not null)
                {
                    department = internalDepartments.FirstOrDefault(option => string.Equals(option.Code, inferredCode, StringComparison.OrdinalIgnoreCase))
                        ?? collectionsDepartment;
                    warnings?.Add("Department was inferred from the imported title.");
                }
                else
                {
                    var linkedDepartment = position?.DepartmentId is Guid linkedId
                        ? internalDepartments.SingleOrDefault(option => option.Id == linkedId)
                        : null;
                    if (linkedDepartment is not null && Matches(position!, departmentSource))
                    {
                        department = linkedDepartment;
                        warnings?.Add("Department was inferred from the selected position.");
                    }
                    else
                    {
                        _ = LookupValue("Department", internalDepartments);
                    }
                }
            }
        }
        else if (department is null)
        {
            var linkedDepartment = position?.DepartmentId is Guid linkedId
                ? internalDepartments.SingleOrDefault(option => option.Id == linkedId)
                : null;
            if (linkedDepartment is not null)
            {
                department = linkedDepartment;
                warnings?.Add("Department was inferred from the selected position.");
            }
            else if (assignedOrganizations.Count > 0 && collectionsDepartment is not null)
            {
                department = collectionsDepartment;
                warnings?.Add("Department was inferred as Collections from the assigned bank/company.");
            }
            else
            {
                _ = LookupValue("Department", internalDepartments);
            }
        }
        else
        {
            var matchedOrganization = FindOrganization(department.Name) ?? FindOrganization(department.Code);
            if (matchedOrganization is not null)
            {
                if (assignedOrganizations.All(item => item.Id != matchedOrganization.Id))
                    assignedOrganizations.Add(matchedOrganization);
                department = collectionsDepartment ?? department;
                warnings?.Add("A bank/company value was recorded as department; the employee was assigned to Collections.");
            }
            else if (position?.DepartmentId is Guid expectedDepartmentId && department.Id != expectedDepartmentId)
            {
                errors.Add("Department does not match the selected position.");
            }
        }

        if (assignedOrganizations.Count > 0 && collectionsDepartment is null)
            errors.Add("Collections department is required to assign a bank or company.");

        var names = EmployeeName.FillMissing(Value("FullName"), Value("FullNameArabic"), Value("FullNameEnglish"));
        if (names.Canonical.Length < 2)
            errors.Add("An Arabic or English employee name is required.");
        if (!EmployeeName.IsArabicName(names.Arabic))
            errors.Add("FullNameArabic: Arabic letters only.");
        if (!EmployeeName.IsEnglishName(names.English))
            errors.Add("FullNameEnglish: English letters only.");

        var gender = Value("Gender").ToLowerInvariant() switch { "" => null, "male" or "m" or "ذكر" => "Male", "female" or "f" or "أنثى" or "انثى" => "Female", _ => "Invalid" };
        if (gender == "Invalid") errors.Add("Gender: invalid value.");
        var status = Value("Status").ToLowerInvariant() switch
        {
            "" => null, "active" or "نشط" => "Active", "inactive" or "غير نشط" => "Inactive",
            "onleave" or "on leave" or "في إجازة" => "OnLeave", "suspended" or "موقوف" => "Suspended",
            "terminated" or "منتهي" => "Terminated", _ => "Invalid"
        };
        if (status == "Invalid") errors.Add("Status: invalid value.");
        var roleRaw = Value("OperationalRole").Trim();
        if (!EmployeeOperationalRoles.TryResolve(roleRaw, position?.Code, position?.Name, position?.Arabic, out var role))
            role = roleRaw.ToUpperInvariant();
        var nationalId = NormalizeNationalId(Value("NationalId"), errors, out var nationalIdBirthDate);
        var mobileNumber = NormalizeEgyptianMobile(Value("MobileNumber"), errors, warnings);
        var workStartDate = Date("WorkStartDate");
        var dateOfBirth = Date("DateOfBirth");
        var fingerprintEnrollmentDate = Date("FingerprintEnrollmentDate");
        var workEndDate = Date("WorkEndDate");
        var basicSalary = Money("BasicSalary");
        var allowances = Money("Allowances");
        if (dateOfBirth.HasValue && nationalIdBirthDate.HasValue && dateOfBirth != nationalIdBirthDate)
            errors.Add("Date of birth does not match the birth date encoded in the National ID.");
        if (dateOfBirth.HasValue && workStartDate.HasValue && workStartDate < dateOfBirth)
            errors.Add("Work start date cannot be before date of birth.");
        if (dateOfBirth.HasValue && fingerprintEnrollmentDate.HasValue && fingerprintEnrollmentDate < dateOfBirth)
            errors.Add("Fingerprint enrollment date cannot be before date of birth.");
        return new SaveEmployeeRequest
        {
            MobileNumber = mobileNumber,
            EmployeeNumber = Value("EmployeeNumber"),
            FullName = names.Canonical,
            FullNameArabic = names.Arabic,
            FullNameEnglish = names.English,
            NationalId = nationalId,
            Gender = gender,
            DepartmentId = department?.Id ?? Guid.Empty,
            OrganizationIds = assignedOrganizations.Select(item => item.Id).Distinct().ToArray(),
            PositionId = position?.Id,
            OperationalRole = role,
            WorkStartDate = workStartDate,
            DateOfBirth = dateOfBirth,
            FingerprintEnrollmentDate = fingerprintEnrollmentDate,
            WorkEndDate = workEndDate,
            Address = Value("Address"),
            Status = status,
            IsActive = status is null or "Active",
            BasicSalary = basicSalary,
            Allowances = allowances,
            WorkNumber = Value("WorkNumber") is { Length: > 0 } workNumber ? workNumber : null,
            PackageType = Value("PackageType") is { Length: > 0 } packageType ? packageType : null
        };
    }

    private static string NormalizeNationalId(string value, List<string> errors, out DateOnly? birthDate)
    {
        birthDate = null;
        var translated = NormalizeDigits(value).Trim();
        var hasInvalidCharacters = translated.Any(character => !char.IsAsciiDigit(character) && !char.IsWhiteSpace(character) && character != '-');
        var normalized = new string(translated.Where(char.IsAsciiDigit).ToArray());
        if (hasInvalidCharacters || normalized.Length != 14)
        {
            errors.Add("National ID must contain exactly 14 digits.");
            return normalized;
        }

        if (normalized[0] is not ('2' or '3'))
        {
            errors.Add("National ID must start with 2 or 3 and contain a valid birth date.");
            return normalized;
        }

        var century = normalized[0] == '2' ? "19" : "20";
        if (!DateOnly.TryParseExact(century + normalized.Substring(1, 6), "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedBirthDate))
        {
            errors.Add("National ID must start with 2 or 3 and contain a valid birth date.");
            return normalized;
        }

        birthDate = parsedBirthDate;
        return normalized;
    }

    private static string? NormalizeEgyptianMobile(string value, List<string> errors, List<string>? warnings)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var translated = NormalizeDigits(value).Trim();
        var hasInvalidCharacters = translated.Any(character => !char.IsAsciiDigit(character) && !char.IsWhiteSpace(character)
            && character is not ('+' or '-' or '(' or ')' or '.'));
        var normalized = new string(translated.Where(char.IsAsciiDigit).ToArray());
        var changed = false;

        if (normalized.Length == 14 && normalized.StartsWith("0020", StringComparison.Ordinal))
        {
            normalized = "0" + normalized[4..];
            changed = true;
        }
        else if (normalized.Length == 12 && normalized.StartsWith("20", StringComparison.Ordinal))
        {
            normalized = "0" + normalized[2..];
            changed = true;
        }
        else if (normalized.Length == 10 && normalized[0] == '1')
        {
            normalized = "0" + normalized;
            warnings?.Add("The missing leading zero was restored in the mobile number.");
        }

        var valid = !hasInvalidCharacters && normalized.Length == 11 && normalized.StartsWith("01", StringComparison.Ordinal)
            && normalized[2] is '0' or '1' or '2' or '5' && normalized.All(char.IsAsciiDigit);
        if (!valid)
            errors.Add("Mobile number must contain exactly 11 digits and use a valid Egyptian mobile prefix.");
        else if (changed)
            warnings?.Add("The mobile number was standardized to the Egyptian local format.");
        return valid ? normalized : normalized.Length > 0 ? normalized : translated;
    }

    private static string NormalizeDigits(string value)
    {
        return string.Concat(value.Select(character => character switch
        {
            '٠' or '۰' => '0', '١' or '۱' => '1', '٢' or '۲' => '2', '٣' or '۳' => '3',
            '٤' or '۴' => '4', '٥' or '۵' => '5', '٦' or '۶' => '6', '٧' or '۷' => '7',
            '٨' or '۸' => '8', '٩' or '۹' => '9', _ => character
        }));
    }

    internal static bool TryDate(string value, string? format, out DateOnly date)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            try { if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)) return true; }
            catch (FormatException) { throw new HrValidationException("Invalid date format."); }
        }
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var native))
        { date = DateOnly.FromDateTime(native); return true; }
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MMM-yy", "d-MMM-yy", "dd-MMM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)) return true;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and <= 2958465)
        { date = DateOnly.FromDateTime(DateTime.FromOADate(serial)); return true; }
        date = default; return false;
    }
}
