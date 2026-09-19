using MIS.Domain.Hr;

namespace MIS.Domain.Constants;

public static class DepartmentCodes
{
    public const string Hr = "HR";
    public const string Legal = "LEGAL";
    public const string Admin = "ADMIN";
    public const string Office = "OFFICE";
    public const string DataEntry = "DATA_ENTRY";
    public const string Accounting = "ACCOUNTING";
    public const string Collections = "COLLECTIONS";

    public static readonly string[] OperationalUnits =
    {
        Hr, Legal, Admin, Office, DataEntry, Accounting, Collections
    };

    public static readonly HashSet<string> LegacyTitleUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIN_ADMIN_DIRECTOR_UNIT",
        "DIRECTOR_UNIT",
        "COLLECTION_MANAGER_UNIT",
        "LOWER",
        "OFFICE_GIRL_UNIT"
    };

    public static bool IsOperationalUnit(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && OperationalUnits.Any(unit => string.Equals(unit, code, StringComparison.OrdinalIgnoreCase));

    public static bool IsLegacyTitleUnit(string? code) =>
        !string.IsNullOrWhiteSpace(code) && LegacyTitleUnits.Contains(code);

    public static string? DirectoryGroup(string? code)
    {
        if (IsOperationalUnit(code))
            return OperationalUnits.First(unit => string.Equals(unit, code, StringComparison.OrdinalIgnoreCase));
        if (!IsLegacyTitleUnit(code)) return null;
        return code!.ToUpperInvariant() switch
        {
            "COLLECTION_MANAGER_UNIT" or "LOWER" => Collections,
            "OFFICE_GIRL_UNIT" => Office,
            _ => Admin
        };
    }

    public static int CompanyDirectoryOrder(string? code)
    {
        var group = DirectoryGroup(code) ?? code;
        var index = Array.FindIndex(OperationalUnits, unit => string.Equals(unit, group, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }

    public static string? InferFromTitle(string? value)
    {
        var normalized = HrOrganizationLookup.Normalize(value);
        return normalized switch
        {
            "collector" or "supervisor" or "collection manager" or "collections manager" or "lower" => Collections,
            "admin" or "director" or "financial administrative director" => Admin,
            "office" or "الاوفيس" or "الأوفيس" or "اوفيس" or "أوفس" or "office boy" or "office girl" or "عامل خدمات" or "عاملة خدمات" => Office,
            "accounting" or "accountant" => Accounting,
            "data entry" or "dataentry" => DataEntry,
            "human resources" or "hr" or "hr officer" or "hr manager" => Hr,
            "legal" => Legal,
            _ => null
        };
    }
}
