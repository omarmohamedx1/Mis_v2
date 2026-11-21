namespace MIS.Domain.Entities;

public sealed class LegalClientAssignment
{
    public const string ArchivedStatus = "ARCHIVED";

    public void Archive(Guid updatedByUserId, DateTimeOffset updatedAt)
        => Update(ArchivedStatus, Notes, updatedByUserId, updatedAt);

    private LegalClientAssignment() { }

    public LegalClientAssignment(Guid customerId, Guid createdByUserId, DateTimeOffset createdAt)
    {
        if (customerId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Customer and creator are required.");

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = "ACTIVE";
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public CollectionCustomer Customer { get; private set; } = null!;
    public string Status { get; private set; } = "ACTIVE";
    public string? Notes { get; private set; }
    public string? ExternalIdentifier { get; private set; }
    public string? ImportedFieldsJson { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public User CreatedByUser { get; private set; } = null!;
    public Guid UpdatedByUserId { get; private set; }
    public User UpdatedByUser { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string status, string? notes, Guid updatedByUserId, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("Updater is required.", nameof(updatedByUserId));
        Status = status.Trim().ToUpperInvariant();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = updatedAt;
    }

    public void SetImportedData(string? externalIdentifier, string? importedFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(ExternalIdentifier) && !string.IsNullOrWhiteSpace(externalIdentifier))
            ExternalIdentifier = externalIdentifier.Trim();
        if (!string.IsNullOrWhiteSpace(importedFieldsJson))
            ImportedFieldsJson = importedFieldsJson;
    }
}

public sealed class LegalClientDocument
{
    private LegalClientDocument() { }

    public LegalClientDocument(
        Guid customerId,
        string originalFileName,
        string contentType,
        long fileSize,
        string fileHash,
        string storageKey,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        if (customerId == Guid.Empty || uploadedByUserId == Guid.Empty)
            throw new ArgumentException("Customer and uploader are required.");
        if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));

        var safeName = originalFileName.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(safeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        Id = Guid.NewGuid();
        CustomerId = customerId;
        OriginalFileName = safeName;
        ContentType = contentType.Trim();
        FileSize = fileSize;
        FileHash = fileHash.Trim();
        StorageKey = storageKey.Trim();
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public CollectionCustomer Customer { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string FileHash { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public User UploadedByUser { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }
}
