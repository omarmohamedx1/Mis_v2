namespace MIS.Domain.Entities;

public sealed class EmployeeDocument
{
    private EmployeeDocument() { }

    public EmployeeDocument(
        Guid employeeId,
        Guid documentTypeId,
        string documentType,
        string fileName,
        string storageKey,
        string mimeType,
        long fileSize,
        string sha256Hash,
        DateOnly? issueDate,
        DateOnly? expiryDate,
        string? notes,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt,
        string? requiredDocumentCode = null)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee is required.", nameof(employeeId));
        if (documentTypeId == Guid.Empty) throw new ArgumentException("Document type is required.", nameof(documentTypeId));
        if (uploadedByUserId == Guid.Empty) throw new ArgumentException("Uploader is required.", nameof(uploadedByUserId));
        Id = Guid.NewGuid();
        EmployeeId = employeeId;
        DocumentTypeId = documentTypeId;
        DocumentType = Required(documentType, nameof(documentType));
        SetMetadata(issueDate, expiryDate, notes);
        SetFile(fileName, storageKey, mimeType, fileSize, sha256Hash);
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
        RequiredDocumentCode = string.IsNullOrWhiteSpace(requiredDocumentCode) ? null : requiredDocumentCode.Trim().ToUpperInvariant();
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Guid? DocumentTypeId { get; private set; }
    public DocumentType? DocumentTypeDefinition { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string? RequiredDocumentCode { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string? Sha256Hash { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? Notes { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public User UploadedByUser { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public User? UpdatedByUser { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public User? DeletedByUser { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeleteReason { get; private set; }

    public void UpdateMetadata(
        Guid documentTypeId,
        string documentType,
        DateOnly? issueDate,
        DateOnly? expiryDate,
        string? notes,
        Guid updatedByUserId,
        DateTimeOffset updatedAt)
    {
        if (documentTypeId == Guid.Empty) throw new ArgumentException("Document type is required.", nameof(documentTypeId));
        EnsureMutable();
        DocumentTypeId = documentTypeId;
        DocumentType = Required(documentType, nameof(documentType));
        SetMetadata(issueDate, expiryDate, notes);
        SetUpdated(updatedByUserId, updatedAt);
    }

    public void ReplaceFile(
        string fileName,
        string storageKey,
        string mimeType,
        long fileSize,
        string sha256Hash,
        Guid updatedByUserId,
        DateTimeOffset updatedAt)
    {
        EnsureMutable();
        SetFile(fileName, storageKey, mimeType, fileSize, sha256Hash);
        SetUpdated(updatedByUserId, updatedAt);
    }

    public void Delete(Guid deletedByUserId, string? reason, DateTimeOffset deletedAt)
    {
        EnsureMutable();
        if (deletedByUserId == Guid.Empty) throw new ArgumentException("Deleting user is required.", nameof(deletedByUserId));
        IsDeleted = true;
        DeletedByUserId = deletedByUserId;
        DeletedAt = deletedAt;
        DeleteReason = Optional(reason, 500);
    }

    private void SetMetadata(DateOnly? issueDate, DateOnly? expiryDate, string? notes)
    {
        if (issueDate.HasValue && expiryDate.HasValue && expiryDate < issueDate)
            throw new ArgumentException("Expiry date cannot be before issue date.", nameof(expiryDate));
        IssueDate = issueDate;
        ExpiryDate = expiryDate;
        Notes = Optional(notes, 1000);
    }

    private void SetFile(string fileName, string storageKey, string mimeType, long fileSize, string sha256Hash)
    {
        if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));
        var hash = Required(sha256Hash, nameof(sha256Hash)).ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A SHA-256 hash is required.", nameof(sha256Hash));
        FileName = Required(fileName, nameof(fileName));
        StorageKey = Required(storageKey, nameof(storageKey));
        MimeType = Required(mimeType, nameof(mimeType));
        FileSize = fileSize;
        Sha256Hash = hash;
    }

    private void SetUpdated(Guid userId, DateTimeOffset timestamp)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Updating user is required.", nameof(userId));
        UpdatedByUserId = userId;
        UpdatedAt = timestamp;
    }

    private void EnsureMutable()
    {
        if (IsDeleted) throw new InvalidOperationException("A deleted document cannot be changed.");
    }

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength) throw new ArgumentException($"Value cannot exceed {maximumLength} characters.");
        return normalized;
    }
}
