namespace MIS.Domain.Entities;

public sealed class BankPortfolioImport
{
    private BankPortfolioImport() { }

    public BankPortfolioImport(Guid bankId, string portfolioName, string originalFileName, string contentType,
        long fileSize, string fileHash, string storageKey, int rowCount, Guid uploadedById, DateTimeOffset uploadedAt)
        : this(bankId, null, portfolioName, originalFileName, contentType, fileSize, fileHash, storageKey, rowCount, uploadedById, uploadedAt)
    {
    }

    public BankPortfolioImport(Guid bankId, Guid? portfolioId, string portfolioName, string originalFileName, string contentType,
        long fileSize, string fileHash, string storageKey, int rowCount, Guid uploadedById, DateTimeOffset uploadedAt)
    {
        if (bankId == Guid.Empty || uploadedById == Guid.Empty) throw new ArgumentException("Bank and uploader are required.");
        if (portfolioId == Guid.Empty) throw new ArgumentException("Portfolio ID cannot be empty.", nameof(portfolioId));
        if (fileSize <= 0 || rowCount <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));
        Id = Guid.NewGuid(); BankId = bankId; PortfolioId = portfolioId; PortfolioName = portfolioName.Trim(); OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim(); FileSize = fileSize; FileHash = fileHash.Trim(); StorageKey = storageKey.Trim();
        RowCount = rowCount; UploadedById = uploadedById; UploadedAt = uploadedAt; Status = "READY";
    }

    public Guid Id { get; private set; }
    public Guid BankId { get; private set; }
    public ClientOrganization Bank { get; private set; } = null!;
    public Guid? PortfolioId { get; private set; }
    public CollectionPortfolio? Portfolio { get; private set; }
    public string PortfolioName { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string FileHash { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public int RowCount { get; private set; }
    public int ImportedRows { get; private set; }
    public int UpdatedRows { get; private set; }
    public int DuplicateRows { get; private set; }
    public int InvalidRows { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid UploadedById { get; private set; }
    public User UploadedBy { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid? ArchivedById { get; private set; }
    public User? ArchivedBy { get; private set; }
    public string? ArchiveReason { get; private set; }
    public string? ArchiveNotes { get; private set; }
    public DateTimeOffset? RestoredAt { get; private set; }
    public Guid? RestoredById { get; private set; }
    public User? RestoredBy { get; private set; }
    public string? RestoreReason { get; private set; }

    public void Confirm(DateTimeOffset confirmedAt)
    {
        if (Status == "COMPLETED") return;
        if (Status != "READY") throw new InvalidOperationException("Only a ready import can be confirmed.");
        Status = "COMPLETED";
        ConfirmedAt = confirmedAt;
    }

    public void Complete(DateTimeOffset confirmedAt, int importedRows, int updatedRows, int duplicateRows, int invalidRows)
    {
        if (importedRows < 0 || updatedRows < 0 || duplicateRows < 0 || invalidRows < 0)
            throw new ArgumentOutOfRangeException(nameof(importedRows));
        Confirm(confirmedAt);
        ImportedRows = importedRows;
        UpdatedRows = updatedRows;
        DuplicateRows = duplicateRows;
        InvalidRows = invalidRows;
    }

    public void UpdateNotes(string? notes, DateTimeOffset updatedAt)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = updatedAt;
    }

    public void ReplaceFile(string originalFileName, string contentType, long fileSize, string fileHash,
        string storageKey, int rowCount, DateTimeOffset updatedAt)
    {
        OriginalFileName = originalFileName.Trim(); ContentType = contentType.Trim(); FileSize = fileSize;
        FileHash = fileHash.Trim(); StorageKey = storageKey.Trim(); RowCount = rowCount; UpdatedAt = updatedAt;
    }
    public void Archive(string reason, string? notes, Guid userId, DateTimeOffset now)
    { if (IsArchived) throw new InvalidOperationException("The portfolio is already archived."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); IsArchived = true; ArchivedAt = now; ArchivedById = userId; ArchiveReason = reason.Trim().ToUpperInvariant(); ArchiveNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); UpdatedAt = now; }
    public void Restore(string reason, Guid userId, DateTimeOffset now)
    { if (!IsArchived) throw new InvalidOperationException("The portfolio is not archived."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); IsArchived = false; RestoredAt = now; RestoredById = userId; RestoreReason = reason.Trim(); UpdatedAt = now; }
}
