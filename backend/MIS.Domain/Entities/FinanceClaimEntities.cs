namespace MIS.Domain.Entities;

public sealed class FinanceClaim
{
    private FinanceClaim() { }

    public FinanceClaim(string? serial, Guid? bankOrganizationId, string bankName, string claimType, string month,
        DateOnly claimDate, decimal amountWithoutVat, decimal vatPercent, decimal taxAtSourcePercent,
        DateOnly? dateInBank, bool importedHistorical, Guid? importBatchId, Guid createdById, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        CreatedById = createdById;
        CreatedAt = createdAt;
        ApplyDetails(serial, bankOrganizationId, bankName, claimType, month, claimDate, amountWithoutVat, vatPercent, taxAtSourcePercent);
        DateInBank = dateInBank;
        Status = importedHistorical && dateInBank.HasValue ? "RECEIVED" : "PENDING";
        IsHistoricalImport = importedHistorical;
        ImportBatchId = importBatchId;
    }

    public Guid Id { get; private set; }
    public string? Serial { get; private set; }
    public Guid? BankOrganizationId { get; private set; }
    public ClientOrganization? BankOrganization { get; private set; }
    public string BankName { get; private set; } = "";
    public string ClaimType { get; private set; } = "";
    public string Month { get; private set; } = "";
    public DateOnly ClaimDate { get; private set; }
    public decimal AmountWithoutVat { get; private set; }
    public decimal VatPercent { get; private set; }
    public decimal Vat { get; private set; }
    public decimal TotalWithVat { get; private set; }
    public decimal TaxAtSourcePercent { get; private set; }
    public decimal TaxAtSource { get; private set; }
    public decimal Total { get; private set; }
    public decimal TotalInBank { get; private set; }
    public DateOnly? DateInBank { get; private set; }
    public string Status { get; private set; } = "PENDING";
    public bool IsHistoricalImport { get; private set; }
    public Guid? ImportBatchId { get; private set; }
    public FinanceClaimImportBatch? ImportBatch { get; private set; }
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? UpdatedById { get; private set; }
    public User? UpdatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? ReceiptConfirmedById { get; private set; }
    public User? ReceiptConfirmedBy { get; private set; }
    public DateTimeOffset? ReceiptConfirmedAt { get; private set; }
    public Guid? CancelledById { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? DeletedById { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public ICollection<FinanceClaimReceiptPosting> ReceiptPostings { get; private set; } = new List<FinanceClaimReceiptPosting>();

    public void SoftDelete(Guid actorId, DateTimeOffset now)
    {
        if (DeletedAt.HasValue) return;
        DeletedById = actorId;
        DeletedAt = now;
    }

    public void ApplyDetails(string? serial, Guid? bankOrganizationId, string bankName, string claimType, string month,
        DateOnly claimDate, decimal amountWithoutVat, decimal vatPercent, decimal taxAtSourcePercent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankName);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(month);
        if (amountWithoutVat <= 0 || decimal.Round(amountWithoutVat, 2) != amountWithoutVat)
            throw new ArgumentOutOfRangeException(nameof(amountWithoutVat), "Claim amount must be positive with at most two decimals.");
        if (vatPercent is < 0 or > 100 || decimal.Round(vatPercent, 4) != vatPercent)
            throw new ArgumentOutOfRangeException(nameof(vatPercent), "VAT percentage must be between 0 and 100.");
        if (taxAtSourcePercent is < 0 or > 100 || decimal.Round(taxAtSourcePercent, 4) != taxAtSourcePercent)
            throw new ArgumentOutOfRangeException(nameof(taxAtSourcePercent), "Withholding percentage must be between 0 and 100.");
        if (claimDate.Year is < 2000 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(claimDate));

        Serial = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim();
        BankOrganizationId = bankOrganizationId;
        BankName = bankName.Trim();
        ClaimType = claimType.Trim();
        Month = month.Trim();
        ClaimDate = claimDate;
        AmountWithoutVat = amountWithoutVat;
        VatPercent = vatPercent;
        Vat = CalculatePercent(amountWithoutVat, vatPercent);
        TotalWithVat = decimal.Round(amountWithoutVat + Vat, 2, MidpointRounding.AwayFromZero);
        TaxAtSourcePercent = taxAtSourcePercent;
        TaxAtSource = CalculatePercent(amountWithoutVat, taxAtSourcePercent);
        Total = decimal.Round(TotalWithVat - TaxAtSource, 2, MidpointRounding.AwayFromZero);
        TotalInBank = Total;
        if (TotalInBank <= 0) throw new ArgumentException("The net bank amount must be positive.");
    }

    public void UpdateDetails(string? serial, Guid? bankOrganizationId, string bankName, string claimType, string month,
        DateOnly claimDate, decimal amountWithoutVat, decimal vatPercent, decimal taxAtSourcePercent, DateOnly? dateInBank, Guid actorId, DateTimeOffset now)
    {
        if (Status != "PENDING") throw new InvalidOperationException("Only a pending claim can be edited. Reverse or reopen its receipt first.");
        ApplyDetails(serial, bankOrganizationId, bankName, claimType, month, claimDate, amountWithoutVat, vatPercent, taxAtSourcePercent);
        DateInBank = dateInBank;
        UpdatedById = actorId;
        UpdatedAt = now;
    }

    public void ConfirmReceipt(DateOnly dateInBank, Guid actorId, DateTimeOffset now)
    {
        if (Status != "PENDING") throw new InvalidOperationException("Only a pending claim can be received.");
        if (dateInBank.Year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(dateInBank));
        DateInBank = dateInBank;
        Status = "RECEIVED";
        ReceiptConfirmedById = actorId;
        ReceiptConfirmedAt = now;
        UpdatedById = actorId;
        UpdatedAt = now;
    }

    public void ReopenAfterReceiptReversal(Guid actorId, DateTimeOffset now)
    {
        if (Status != "RECEIVED") throw new InvalidOperationException("Only a received claim can be reopened.");
        Status = "PENDING";
        DateInBank = null;
        ReceiptConfirmedById = null;
        ReceiptConfirmedAt = null;
        UpdatedById = actorId;
        UpdatedAt = now;
    }

    public void Cancel(Guid actorId, DateTimeOffset now)
    {
        if (Status == "CANCELLED") return;
        if (Status is not ("PENDING" or "RECEIVED")) throw new InvalidOperationException("This claim cannot be cancelled.");
        Status = "CANCELLED";
        CancelledById = actorId;
        CancelledAt = now;
        UpdatedById = actorId;
        UpdatedAt = now;
    }

    private static decimal CalculatePercent(decimal amount, decimal percent)
        => decimal.Round(amount * percent / 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed class FinanceClaimReceiptPosting
{
    private FinanceClaimReceiptPosting() { }

    public FinanceClaimReceiptPosting(Guid id, Guid claimId, Guid accountingBookEntryId, decimal amount, DateOnly receiptDate,
        bool historical, Guid actorId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || claimId == Guid.Empty || accountingBookEntryId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Receipt posting identities are required.");
        Id = id;
        ClaimId = claimId;
        AccountingBookEntryId = accountingBookEntryId;
        Amount = amount;
        ReceiptDate = receiptDate;
        IsHistoricalPosting = historical;
        CreatedById = actorId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public FinanceClaim Claim { get; private set; } = null!;
    public Guid AccountingBookEntryId { get; private set; }
    public AccountingBookEntry AccountingBookEntry { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateOnly ReceiptDate { get; private set; }
    public bool IsHistoricalPosting { get; private set; }
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ReversedById { get; private set; }
    public User? ReversedBy { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public Guid? ReversalJournalId { get; private set; }

    public void MarkReversed(Guid actorId, DateTimeOffset now, Guid? reversalJournalId)
    {
        if (ReversedAt.HasValue) return;
        ReversedById = actorId;
        ReversedAt = now;
        ReversalJournalId = reversalJournalId;
    }
}

public sealed class FinanceClaimImportBatch
{
    private FinanceClaimImportBatch() { }

    public FinanceClaimImportBatch(string fileName, string fileHash, string previewJson, int totalRows, Guid actorId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        FileName = fileName;
        FileHash = fileHash;
        PreviewJson = previewJson;
        TotalRows = totalRows;
        CreatedById = actorId;
        CreatedAt = createdAt;
        ExpiresAt = createdAt.AddHours(24);
    }

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = "";
    public string FileHash { get; private set; } = "";
    public string PreviewJson { get; private set; } = "[]";
    public int TotalRows { get; private set; }
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public Guid? ConfirmedById { get; private set; }
    public User? ConfirmedBy { get; private set; }

    public void Confirm(Guid actorId, DateTimeOffset now)
    {
        if (ConfirmedAt.HasValue) throw new InvalidOperationException("This import was already confirmed.");
        if (ExpiresAt <= now) throw new InvalidOperationException("This import preview has expired. Upload the workbook again.");
        ConfirmedById = actorId;
        ConfirmedAt = now;
    }
}
