using System.ComponentModel.DataAnnotations;

namespace MIS.Application.Common;

public static class EmployeeMobileNumber
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var mobile = value.Trim();
        if (mobile.Length > 32 || !new PhoneAttribute().IsValid(mobile))
            throw new HrValidationException("Mobile number must be a valid phone number of at most 32 characters.");
        return mobile;
    }
}
