namespace MIS.Domain.Hr;

public static class EmployeeOperationalRoles
{
    public const string Collector = "COLLECTOR";
    public const string Admin = "ADMIN";
    public const string Supervisor = "SUPERVISOR";
    public const string Office = "OFFICE";

    public static bool TryNormalize(string? value, out string? role)
    {
        role = value?.Trim().ToUpperInvariant() switch
        {
            Collector => Collector,
            Admin => Admin,
            Supervisor => Supervisor,
            Office => Office,
            _ => null
        };
        return role is not null;
    }

    public static string InferFromPosition(string? code, string? name, string? nameArabic)
    {
        var text = $" {HrOrganizationLookup.Normalize($"{code} {name} {nameArabic}")} ";
        if (ContainsAny(text, "office boy", "office girl", "عامل خدمات", "عاملة خدمات", "اوفيس", "أوفيس", "أوفس", "الأوفيس", "الاوفيس", " office "))
            return Office;
        if (ContainsAny(text, "supervisor", "مشرف", "collection manager", "collections manager", "مدير تحصيل", "مدير التحصيل"))
            return Supervisor;
        if (ContainsAny(text, "collector", "محصل"))
            return Collector;
        return Admin;
    }

    public static bool TryResolve(string? requested, string? code, string? name, string? nameArabic, out string role)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            role = InferFromPosition(code, name, nameArabic);
            return true;
        }

        if (TryNormalize(requested, out var normalized) && normalized is not null)
        {
            role = normalized;
            return true;
        }

        var inferredFromRequested = InferFromPosition(requested, requested, requested);
        if (inferredFromRequested != Admin || IsAdminAlias(requested))
        {
            role = inferredFromRequested;
            return true;
        }

        role = string.Empty;
        return false;
    }

    private static bool IsAdminAlias(string? value)
    {
        var text = HrOrganizationLookup.Normalize(value);
        return text is "admin" or "administrator" or "اداري" or "إداري"
            or "data entry" or "dataentry" or "إدخال البيانات" or "ادخال البيانات"
            or "accounting" or "accountant" or "الحسابات" or "محاسب"
            or "hr" or "hr officer" or "hr manager" or "human resources" or "الموارد البشرية"
            or "legal" or "الشؤون القانونية";
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(HrOrganizationLookup.Normalize(needle), StringComparison.Ordinal));
}
