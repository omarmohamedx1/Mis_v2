using MIS.Application.DTOs.Hr;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeDocumentBulkMapperTests
{
    private static readonly EmployeeDocumentBulkMapper.EmployeeMatch Nada = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "1001",
        "29501011234567",
        "01012345678",
        "Nada Ali",
        "ندى علي",
        "Nada Ali",
        "Female");

    private static readonly EmployeeDocumentBulkMapper.EmployeeMatch Ola = new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "1002",
        "30308110102789",
        "01098765432",
        "Ola Ahmed",
        "علا أحمد",
        "Ola Ahmed",
        "Female");

    private static readonly EmployeeDocumentBulkMapper.EmployeeMatch NadaTwo = new(
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "1003",
        "29501019876543",
        "01111111111",
        "Nada Hassan",
        "ندى حسن",
        "Nada Hassan",
        "Female");

    [Theory]
    [InlineData("1001_nada_birth_certificate.pdf", "1001", "BIRTH_CERTIFICATE")]
    [InlineData("30308110102789_ola_national_id.jpg", null, "NATIONAL_ID_COPY")]
    [InlineData("1001_ندى_شهادة_الميلاد.pdf", "1001", "BIRTH_CERTIFICATE")]
    [InlineData("1001_Nada_CriminalRecord.pdf", "1001", "CRIMINAL_RECORD")]
    [InlineData("1002_Ola_KaabElAmal.pdf", "1002", "LABOR_OFFICE_REGISTRATION")]
    [InlineData("1001_Nada_MilitaryStatus.pdf", "1001", "MILITARY_STATUS")]
    public void ParseFileName_detects_employee_and_document(string fileName, string? expectedNumber, string expectedCode)
    {
        var parsed = EmployeeDocumentBulkMapper.ParseFileName(fileName);
        Assert.Equal(expectedNumber, parsed.EmployeeNumber);
        Assert.Equal(expectedCode, parsed.DocumentCode);
        if (fileName.StartsWith("30308110102789"))
            Assert.Equal("30308110102789", parsed.NationalId);
    }

    [Fact]
    public void MapItem_matches_employee_number_and_marks_ready()
    {
        var item = Map("1001_Nada_BirthCertificate.pdf");
        Assert.Equal("Ready", item.Status);
        Assert.Equal(Nada.Id, item.EmployeeId);
        Assert.Equal(RequiredEmployeeDocumentCodes.BirthCertificate, item.DocumentCode);
    }

    [Fact]
    public void MapItem_matches_national_id()
    {
        var item = Map("30308110102789_ola_national_id.jpg");
        Assert.Equal("Ready", item.Status);
        Assert.Equal(Ola.Id, item.EmployeeId);
        Assert.Equal(RequiredEmployeeDocumentCodes.NationalIdCopy, item.DocumentCode);
    }

    [Fact]
    public void MapItem_matches_mobile_number()
    {
        var parsed = EmployeeDocumentBulkMapper.ParseFileName("01012345678_birth.pdf");
        Assert.Equal("01012345678", parsed.MobileNumber);
        var item = Map("01012345678_birth.pdf");
        Assert.Equal("Ready", item.Status);
        Assert.Equal(Nada.Id, item.EmployeeId);
    }

    [Fact]
    public void MapItem_unique_name_match_works()
    {
        var item = Map("ola_ahmed_graduation.pdf", employees: [Ola, Nada]);
        Assert.Equal("Ready", item.Status);
        Assert.Equal(Ola.Id, item.EmployeeId);
        Assert.Equal(RequiredEmployeeDocumentCodes.GraduationCertificate, item.DocumentCode);
    }

    [Fact]
    public void MapItem_ambiguous_name_is_not_guessed()
    {
        var item = Map("nada_birth.pdf", employees: [Nada, NadaTwo, Ola]);
        Assert.Equal("AmbiguousEmployee", item.Status);
        Assert.Null(item.EmployeeId);
    }

    [Fact]
    public void MapItem_marks_existing_document()
    {
        var existing = new HashSet<(Guid, string)> { (Nada.Id, RequiredEmployeeDocumentCodes.BirthCertificate) };
        var item = Map("1001_Nada_BirthCertificate.pdf", existing: existing);
        Assert.Equal("DocumentAlreadyExists", item.Status);
        Assert.Equal("Skip", item.DuplicateAction);
    }

    [Fact]
    public void MapItem_manual_override_fixes_unknown_file()
    {
        var item = EmployeeDocumentBulkMapper.MapItem(
            new EmployeeDocumentBulkMapper.StoredFile(Guid.NewGuid(), "scan001.pdf", "key", "application/pdf", 10),
            EmployeeDocumentBulkMapper.ParseFileName("scan001.pdf"),
            [Nada, Ola],
            [],
            [],
            Nada.Id,
            RequiredEmployeeDocumentCodes.CriminalRecord,
            "Skip",
            false);

        Assert.Equal("Ready", item.Status);
        Assert.Equal(Nada.Id, item.EmployeeId);
        Assert.Equal(RequiredEmployeeDocumentCodes.CriminalRecord, item.DocumentCode);
    }

    private static EmployeeDocumentBulkItem Map(
        string fileName,
        IReadOnlyCollection<EmployeeDocumentBulkMapper.EmployeeMatch>? employees = null,
        HashSet<(Guid, string)>? existing = null)
    {
        return EmployeeDocumentBulkMapper.MapItem(
            new EmployeeDocumentBulkMapper.StoredFile(Guid.NewGuid(), fileName, "key", "application/pdf", 10),
            EmployeeDocumentBulkMapper.ParseFileName(fileName),
            employees ?? [Nada, Ola, NadaTwo],
            existing ?? [],
            [],
            null,
            null,
            "Skip",
            false);
    }
}
