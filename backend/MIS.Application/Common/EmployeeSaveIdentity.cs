using MIS.Application.DTOs.Hr;
using MIS.Domain.Hr;

namespace MIS.Application.Common;

public static class EmployeeSaveIdentity
{
    public static SaveEmployeeRequest Normalize(SaveEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NationalId))
            throw new HrValidationException("The NationalId field is required.");
        if (!EgyptianHrDataValidator.TryParseNationalId(request.NationalId, out var parsed, out var error))
            throw new HrValidationException(error);

        var dateOfBirth = request.DateOfBirth ?? parsed.DateOfBirth;
        var gender = string.IsNullOrWhiteSpace(request.Gender)
            ? (parsed.Gender == "female" ? "Female" : "Male")
            : request.Gender;
        var nationalId = EgyptianHrDataValidator.NormalizeNationalId(parsed.NationalId, dateOfBirth, gender)
            ?? throw new HrValidationException("The NationalId field is required.");
        var mobile = EgyptianHrDataValidator.NormalizePhone(request.MobileNumber, "Mobile number");
        if (mobile is not null &&
            (mobile.Length != 11 || !mobile.StartsWith("01", StringComparison.Ordinal)))
            throw new HrValidationException("Phone number must be a valid Egyptian mobile number (010, 011, 012, or 015).");
        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new HrValidationException("Date of birth cannot be in the future.");
        if (request.WorkStartDate.HasValue && request.WorkStartDate < dateOfBirth)
            throw new HrValidationException("Work start date cannot be before date of birth.");
        if (request.FingerprintEnrollmentDate.HasValue && request.FingerprintEnrollmentDate < dateOfBirth)
            throw new HrValidationException("Fingerprint enrollment date cannot be before date of birth.");
        if (request.WorkEndDate.HasValue && request.WorkStartDate.HasValue && request.WorkEndDate < request.WorkStartDate)
            throw new HrValidationException("Work end date cannot be before work start date.");
        if (request.BasicSalary < 0 || request.Allowances < 0)
            throw new HrValidationException("Salary and allowances cannot be negative.");

        var names = EmployeeName.FillMissing(request.FullName, request.FullNameArabic, request.FullNameEnglish);
        string? arabic;
        string? english;
        try { arabic = EmployeeName.RequireArabic(names.Arabic); }
        catch (ArgumentException invalidName) { throw new HrValidationException(invalidName.Message); }
        try { english = EmployeeName.RequireEnglish(names.English); }
        catch (ArgumentException invalidName) { throw new HrValidationException(invalidName.Message); }

        return new SaveEmployeeRequest
        {
            EmployeeNumber = request.EmployeeNumber.Trim(),
            FullName = names.Canonical,
            FullNameArabic = arabic,
            FullNameEnglish = english,
            NationalId = nationalId,
            MobileNumber = mobile,
            DepartmentId = request.DepartmentId,
            IsActive = request.IsActive,
            Gender = gender,
            Status = request.Status,
            PositionId = request.PositionId,
            OperationalRole = request.OperationalRole,
            WorkStartDate = request.WorkStartDate,
            FingerprintEnrollmentDate = request.FingerprintEnrollmentDate,
            DateOfBirth = dateOfBirth,
            Address = request.Address,
            WorkEndDate = request.WorkEndDate,
            OrganizationIds = request.OrganizationIds,
            BasicSalary = request.BasicSalary,
            Allowances = request.Allowances,
            WorkNumber = string.IsNullOrWhiteSpace(request.WorkNumber) ? null : request.WorkNumber.Trim(),
            PackageType = string.IsNullOrWhiteSpace(request.PackageType) ? null : request.PackageType.Trim()
        };
    }
}
