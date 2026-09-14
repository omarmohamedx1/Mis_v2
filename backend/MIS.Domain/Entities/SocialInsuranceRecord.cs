namespace MIS.Domain.Entities;

public sealed class SocialInsuranceRecord
{
    private SocialInsuranceRecord() { }
    public SocialInsuranceRecord(Guid employeeId, string number, DateOnly? start, decimal salary, string status, string? office, string? reference, string? notes)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee is required.");
        Id = Guid.NewGuid(); EmployeeId = employeeId; CreatedAt = DateTimeOffset.UtcNow;
        Update(number, start, salary, status, office, reference, notes);
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public string SocialInsuranceNumber { get; private set; } = "";
    public DateOnly? InsuranceStartDate { get; private set; }
    public DateOnly? InsuranceEndDate { get; private set; }
    public decimal InsurableSalary { get; private set; }
    public string InsuranceStatus { get; private set; } = "NotInsured";
    public string? InsuranceOffice { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static string NormalizeNumber(string number) => string.Concat((number ?? "").Trim().Select(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DecimalDigitNumber ? (char)('0' + (int)char.GetNumericValue(c)) : char.ToUpperInvariant(c)));
    public void Update(string number, DateOnly? start, decimal salary, string status, string? office, string? reference, string? notes)
    {
        if (InsuranceStatus == "Ended") throw new InvalidOperationException("Ended insurance records cannot be edited.");
        if (status is not ("Insured" or "NotInsured" or "Suspended")) throw new ArgumentException("Invalid insurance status.");
        number = NormalizeNumber(number);
        if (number.Length == 0 || number.Length > 50) throw new ArgumentException("Social insurance number is required and must not exceed 50 characters.");
        if ((status is "Insured" or "Suspended") && (start is null || start == default(DateOnly))) throw new ArgumentException("Insurance start date is required.");
        if (salary <= 0 || salary > 9999999999999999.99m || decimal.Round(salary, 2) != salary) throw new ArgumentException("Insurable salary must be positive with at most two decimal places.");
        if (office?.Length > 160 || reference?.Length > 100 || notes?.Length > 2000) throw new ArgumentException("Insurance text exceeds the maximum length.");
        SocialInsuranceNumber = number; InsuranceStartDate = start; InsurableSalary = salary; InsuranceStatus = status;
        InsuranceOffice = office?.Trim(); ReferenceNumber = reference?.Trim(); Notes = notes?.Trim(); UpdatedAt = DateTimeOffset.UtcNow;
    }
    public void End(DateOnly end)
    {
        if (InsuranceStatus == "Ended") throw new InvalidOperationException("Insurance has already ended.");
        if (end == default || end < InsuranceStartDate) throw new ArgumentException("Insurance end date is required and cannot precede its start date.");
        InsuranceEndDate = end; InsuranceStatus = "Ended"; UpdatedAt = DateTimeOffset.UtcNow;
    }
}
