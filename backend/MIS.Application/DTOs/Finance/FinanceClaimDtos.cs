using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Finance;

public sealed record FinanceClaimFilter(
    string? Search = null,
    Guid? BankOrganizationId = null,
    string? ClaimType = null,
    string? Month = null,
    string? Status = null,
    DateOnly? ClaimDateFrom = null,
    DateOnly? ClaimDateTo = null,
    DateOnly? BankDateFrom = null,
    DateOnly? BankDateTo = null,
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    int Page = 1,
    int PageSize = 30);

public sealed record SaveFinanceClaimRequest(
    [MaxLength(40)] string? Serial,
    Guid? BankOrganizationId,
    [Required, MaxLength(200)] string BankName,
    [Required, MaxLength(200)] string ClaimType,
    [Required, MaxLength(80)] string Month,
    DateOnly ClaimDate,
    [Range(typeof(decimal), "0.01", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal AmountWithoutVat,
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal VatPercent,
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal TaxAtSourcePercent,
    DateOnly? DateInBank = null);

public sealed record ConfirmFinanceClaimReceiptRequest(DateOnly? DateInBank);
public sealed record ReverseFinanceClaimReceiptRequest([Required, MaxLength(1000)] string Reason);
public sealed record FinanceClaimListItem(
    Guid Id, string? Serial, Guid? BankOrganizationId, string BankName, string ClaimType, string Month, DateOnly ClaimDate,
    decimal AmountWithoutVat, decimal VatPercent, decimal Vat, decimal TotalWithVat, decimal TaxAtSourcePercent, decimal TaxAtSource,
    decimal Total, decimal TotalInBank, DateOnly? DateInBank, string Status, bool IsHistoricalImport, bool HasAccountingPosting,
    string CreatedBy, DateTimeOffset CreatedAt, string? UpdatedBy, DateTimeOffset? UpdatedAt);
public sealed record FinanceClaimSummary(
    int TotalClaims, int PendingClaims, int ReceivedClaims, decimal TotalClaimAmount, decimal TotalReceived,
    decimal NetReceivedInBank, decimal TotalVat, decimal TotalWithholdingTax);
public sealed record FinanceClaimPage(
    IReadOnlyList<FinanceClaimListItem> Items, int Page, int PageSize, int TotalCount, FinanceClaimSummary Summary);
public sealed record FinanceClaimReceiptDto(
    Guid Id, decimal Amount, DateOnly ReceiptDate, bool IsHistoricalPosting, string CreatedBy, DateTimeOffset CreatedAt,
    string? AccountingDocumentNumber, string? AccountingStatus, DateTimeOffset? ReversedAt, string? ReversedBy, Guid? ReversalJournalId);
public sealed record FinanceClaimAuditDto(string Action, string? Actor, string? Reason, DateTimeOffset CreatedAt, string BeforeJson, string AfterJson);
public sealed record FinanceClaimDetails(
    FinanceClaimListItem Claim, IReadOnlyList<FinanceClaimReceiptDto> ReceiptPostings, IReadOnlyList<FinanceClaimAuditDto> AuditHistory,
    string? ImportFileName = null, string? ImportedBy = null, DateTimeOffset? ImportedAt = null,
    string? ReceiptConfirmedBy = null, DateTimeOffset? ReceiptConfirmedAt = null);
public sealed record FinanceClaimOrganization(Guid Id, string Code, string NameArabic, string NameEnglish, string OrganizationType);
public sealed record FinanceClaimImportRow(
    int RowNumber, string? Serial, string? BankName, string? ClaimType, string? Month, DateOnly? ClaimDate,
    decimal? AmountWithoutVat, decimal? VatPercent, decimal? ExcelVat, decimal? ExcelTotalWithVat,
    decimal? TaxAtSourcePercent, decimal? ExcelTaxAtSource, decimal? ExcelTotal, decimal? ExcelTotalInBank,
    DateOnly? DateInBank, decimal? CalculatedVat, decimal? CalculatedTotalWithVat, decimal? CalculatedTaxAtSource,
    decimal? CalculatedTotal, decimal? CalculatedTotalInBank, bool IsValid, bool PossibleDuplicate,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
public sealed record FinanceClaimImportPreview(
    Guid PreviewId, string FileName, int TotalRows, int ValidRows, int WarningRows, int InvalidRows,
    IReadOnlyList<FinanceClaimImportRow> Rows);
public sealed record FinanceClaimImportConfirmRequest(Guid PreviewId);
public sealed record FinanceClaimImportResult(int TotalRows, int Imported, int Skipped, int Failed);
