using MIS.Application.DTOs.Legal;
using MIS.Domain.Entities;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientManagementTests
{
    [Fact]
    public void Legal_import_fills_missing_customer_data_without_overwriting_existing_data()
    {
        var customer = new CollectionCustomer(Guid.NewGuid(), "C-1", "الاسم الحالي", "Existing Name", DateTimeOffset.UtcNow);
        customer.ApplyImportedContact(null, null, "12345678901234", "01000000000");
        customer.FillMissingLegalImportDetails("اسم جديد", "New Name", "99999999999999", "01111111111", null, "New Address");

        Assert.Equal("Existing Name", customer.FullNameEnglish);
        Assert.Equal("12345678901234", customer.NationalId);
        Assert.Equal("01000000000", customer.PrimaryPhone);
        Assert.Equal("New Address", customer.AddressEnglish);
    }

    [Fact]
    public void Legal_client_assignment_tracks_status_and_notes_for_the_existing_customer()
    {
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignment = new LegalClientAssignment(customerId, userId, DateTimeOffset.UtcNow);

        assignment.Update("ON_HOLD", "  Review documents  ", userId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(customerId, assignment.CustomerId);
        Assert.Equal("ON_HOLD", assignment.Status);
        Assert.Equal("Review documents", assignment.Notes);
    }

    [Fact]
    public void Legal_import_data_is_retained_when_a_later_import_has_no_source_fields()
    {
        var assignment = new LegalClientAssignment(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        const string importedRecords = "[{\"Installment Unique ID\":\"0001\"}]";

        assignment.SetImportedData("0001", importedRecords);
        assignment.SetImportedData(null, null);

        Assert.Equal("0001", assignment.ExternalIdentifier);
        Assert.Equal(importedRecords, assignment.ImportedFieldsJson);
    }

    [Fact]
    public void Legal_import_duplicate_detection_uses_both_identifiers_and_the_selected_organization()
    {
        var source = new LegalClientImportSourceRow(
            2, "Customer", null, null, " national-1 ", null, null, null, null, null, null,
            " installment-1 ",
            new Dictionary<string, string> { ["Installment Unique ID"] = "installment-1" });
        var organizationId = Guid.NewGuid();

        var keys = LegalClientService.ImportDuplicateKeys(source, organizationId).ToArray();
        var otherOrganizationKeys = LegalClientService.ImportDuplicateKeys(source, Guid.NewGuid()).ToArray();

        Assert.Contains($"{organizationId}:N:national-1", keys);
        Assert.Contains($"{organizationId}:E:installment-1", keys);
        Assert.DoesNotContain(keys, key => otherOrganizationKeys.Contains(key));
    }

    [Fact]
    public void Legal_client_document_stores_only_a_safe_file_name()
    {
        var document = new LegalClientDocument(
            Guid.NewGuid(),
            @"C:\client-files\identity.pdf",
            "application/pdf",
            128,
            "hash",
            "legal-client-documents/identity.pdf",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Equal("identity.pdf", document.OriginalFileName);
    }
}
