using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EgyptianHrDataValidatorTests
{
    [Fact]
    public void National_id_is_normalized_and_checked_against_birth_date_and_gender()
    {
        var result = EgyptianHrDataValidator.NormalizeNationalId(
            "٢٩٨٠١٠١٠١٢٣٤٥٦",
            new DateOnly(1998, 1, 1),
            "Male");

        Assert.Equal("29801010123456", result);
        Assert.True(EgyptianHrDataValidator.TryParseNationalId("٢٩٨٠١٠١٠١٢٣٤٥٦", out var parsed, out var error));
        Assert.Equal("29801010123456", parsed.NationalId);
        Assert.Equal(new DateOnly(1998, 1, 1), parsed.DateOfBirth);
        Assert.Equal("male", parsed.Gender);
        Assert.Equal(string.Empty, error);
        Assert.False(EgyptianHrDataValidator.TryParseNationalId("19801010123456", out _, out var centuryError));
        Assert.Equal("Egyptian national ID has an invalid century digit.", centuryError);
        Assert.Throws<HrValidationException>(() => EgyptianHrDataValidator.NormalizeNationalId(
            "29801010123456",
            new DateOnly(1998, 1, 2),
            "Male"));
        Assert.Throws<HrValidationException>(() => EgyptianHrDataValidator.NormalizeNationalId(
            "29801010123456",
            new DateOnly(1998, 1, 1),
            "Female"));
    }

    [Theory]
    [InlineData("010 1234 5678", "01012345678")]
    [InlineData("+20 10 1234 5678", "01012345678")]
    [InlineData("0020-11-1234-5678", "01112345678")]
    public void Egyptian_mobile_numbers_are_stored_in_one_consistent_format(string input, string expected)
    {
        Assert.Equal(expected, EgyptianHrDataValidator.NormalizePhone(input, "Mobile number"));
    }

    [Fact]
    public void Invalid_egyptian_mobile_prefix_is_rejected()
    {
        Assert.Throws<HrValidationException>(() =>
            EgyptianHrDataValidator.NormalizePhone("01312345678", "Mobile number"));
    }

    [Fact]
    public void Egyptian_iban_length_and_checksum_are_validated()
    {
        Assert.Equal(
            "EG170001000000000012345678901",
            EgyptianHrDataValidator.NormalizeIban("EG17 0001 0000 0000 0012 3456 7890 1"));
        Assert.Throws<HrValidationException>(() =>
            EgyptianHrDataValidator.NormalizeIban("EG180001000000000012345678901"));
    }

    [Fact]
    public void Employee_save_fills_birth_date_from_national_id_and_rejects_mismatch()
    {
        var request = new SaveEmployeeRequest
        {
            EmployeeNumber = "E-1",
            FullName = "Test Employee",
            NationalId = "28712010111213",
            DepartmentId = Guid.NewGuid(),
        };

        var normalized = EmployeeSaveIdentity.Normalize(request);
        Assert.Equal(new DateOnly(1987, 12, 1), normalized.DateOfBirth);
        Assert.Equal("Male", normalized.Gender);
        Assert.Equal("28712010111213", normalized.NationalId);

        var mismatch = new SaveEmployeeRequest
        {
            EmployeeNumber = "E-1",
            FullName = "Test Employee",
            NationalId = "28712010111213",
            DateOfBirth = new DateOnly(1987, 12, 2),
            DepartmentId = Guid.NewGuid(),
        };
        var error = Assert.Throws<HrValidationException>(() => EmployeeSaveIdentity.Normalize(mismatch));
        Assert.Equal("Date of birth does not match the Egyptian national ID.", error.Message);
    }

    [Theory]
    [InlineData("55")]
    [InlineData("01312345678")]
    [InlineData("+15551234567")]
    public void Employee_save_rejects_non_egyptian_mobile_numbers(string mobile)
    {
        var request = new SaveEmployeeRequest
        {
            EmployeeNumber = "E-1",
            FullName = "Test Employee",
            NationalId = "28712010111213",
            MobileNumber = mobile,
            DepartmentId = Guid.NewGuid(),
        };
        var error = Assert.Throws<HrValidationException>(() => EmployeeSaveIdentity.Normalize(request));
        Assert.Equal("Phone number must be a valid Egyptian mobile number (010, 011, 012, or 015).", error.Message);
    }
}
