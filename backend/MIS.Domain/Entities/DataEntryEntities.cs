using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class DataEntryBatch
{
    private DataEntryBatch() { }

    public DataEntryBatch(
        string batchNumber,
        Guid organizationId,
        Guid portfolioId,
        string source,
        Guid uploadedByUserId,
        DateTimeOffset createdAt,
        string? primaryClassification = null,
        string? subClassification = null,
        string? fileName = null,
        string? storageKey = null)
    {
        if (organizationId == Guid.Empty || portfolioId == Guid.Empty || uploadedByUserId == Guid.Empty)
            throw new ArgumentException("Organization, portfolio, and uploader are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(batchNumber);
        source = source.Trim().ToUpperInvariant();
        if (source is not (DataEntryValues.Sources.Manual or DataEntryValues.Sources.Imported))
            throw new ArgumentException("Invalid data entry source.", nameof(source));

        Id = Guid.NewGuid();
        BatchNumber = batchNumber.Trim().ToUpperInvariant();
        OrganizationId = organizationId;
        PortfolioId = portfolioId;
        PrimaryClassification = NullIfEmpty(primaryClassification)?.ToUpperInvariant();
        SubClassification = NullIfEmpty(subClassification)?.ToUpperInvariant();
        Source = source;
        FileName = NullIfEmpty(fileName);
        StorageKey = NullIfEmpty(storageKey);
        Status = DataEntryValues.BatchStatuses.Draft;
        UploadedByUserId = uploadedByUserId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string BatchNumber { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public Guid PortfolioId { get; private set; }
    public CollectionPortfolio Portfolio { get; private set; } = null!;
    public string? PrimaryClassification { get; private set; }
    public string? SubClassification { get; private set; }
    public string Source { get; private set; } = DataEntryValues.Sources.Manual;
    public string? FileName { get; private set; }
    public string? StorageKey { get; private set; }
    public string Status { get; private set; } = DataEntryValues.BatchStatuses.Draft;
    public int TotalRows { get; private set; }
    public int ValidRows { get; private set; }
    public int InvalidRows { get; private set; }
    public int CreatedCustomerCount { get; private set; }
    public int CreatedCaseCount { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public User UploadedByUser { get; private set; } = null!;
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public User? ReviewedByUser { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? DistributedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<DataEntryRow> Rows { get; private set; } = new List<DataEntryRow>();
    public ICollection<DataEntryNotification> Notifications { get; private set; } = new List<DataEntryNotification>();
    public ICollection<DataEntryDocument> Documents { get; private set; } = new List<DataEntryDocument>();

    public void SetCounts(int total, int valid, int invalid, DateTimeOffset now)
    {
        TotalRows = total; ValidRows = valid; InvalidRows = invalid; UpdatedAt = now;
    }

    public void MarkSubmitted(DateTimeOffset now)
    {
        if (Status != DataEntryValues.BatchStatuses.Draft)
            throw new InvalidOperationException("Only draft batches can be submitted.");
        if (ValidRows <= 0) throw new InvalidOperationException("A batch with no valid rows cannot be submitted.");
        Status = DataEntryValues.BatchStatuses.Submitted;
        SubmittedAt = now;
        UpdatedAt = now;
    }

    public void Accept(Guid reviewerId, DateTimeOffset now)
    {
        if (Status != DataEntryValues.BatchStatuses.Submitted)
            throw new InvalidOperationException("Only submitted batches can be accepted.");
        Status = DataEntryValues.BatchStatuses.Accepted;
        ReviewedByUserId = reviewerId;
        ReviewedAt = now;
        RejectionReason = null;
        UpdatedAt = now;
    }

    public void Reject(Guid reviewerId, string reason, DateTimeOffset now)
    {
        if (Status != DataEntryValues.BatchStatuses.Submitted)
            throw new InvalidOperationException("Only submitted batches can be rejected.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = DataEntryValues.BatchStatuses.Rejected;
        ReviewedByUserId = reviewerId;
        ReviewedAt = now;
        RejectionReason = reason.Trim();
        UpdatedAt = now;
    }

    public void MarkDistributed(DateTimeOffset now)
    {
        if (Status != DataEntryValues.BatchStatuses.Accepted)
            throw new InvalidOperationException("Only accepted batches can be sent to distribution.");
        Status = DataEntryValues.BatchStatuses.Distributed;
        DistributedAt = now;
        UpdatedAt = now;
    }

    public void AddCreatedCounts(int customers, int cases, DateTimeOffset now)
    {
        CreatedCustomerCount += customers;
        CreatedCaseCount += cases;
        UpdatedAt = now;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DataEntryRow
{
    private DataEntryRow() { }

    public DataEntryRow(
        Guid batchId,
        int rowNumber,
        string customerName,
        string rowStatus,
        string? customerCode = null,
        string? nationalId = null,
        string? mobileNumber = null,
        string? address = null,
        string? feedback = null,
        string? notes = null,
        string? accountNumber = null,
        string? contractNumber = null,
        decimal? outstandingBalance = null,
        decimal? overdueBalance = null,
        int? daysPastDue = null,
        string? errorMessage = null)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Batch is required.", nameof(batchId));
        if (rowNumber < 1) throw new ArgumentOutOfRangeException(nameof(rowNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(customerName);
        rowStatus = rowStatus.Trim().ToUpperInvariant();
        if (!DataEntryValues.RowStatuses.All.Contains(rowStatus)) throw new ArgumentException("Invalid row status.", nameof(rowStatus));

        Id = Guid.NewGuid();
        BatchId = batchId;
        RowNumber = rowNumber;
        CustomerCode = NullIfEmpty(customerCode);
        CustomerName = customerName.Trim();
        NationalId = NullIfEmpty(nationalId);
        MobileNumber = NullIfEmpty(mobileNumber);
        Address = NullIfEmpty(address);
        Feedback = NullIfEmpty(feedback);
        Notes = NullIfEmpty(notes);
        AccountNumber = NullIfEmpty(accountNumber);
        ContractNumber = NullIfEmpty(contractNumber);
        OutstandingBalance = outstandingBalance;
        OverdueBalance = overdueBalance;
        DaysPastDue = daysPastDue;
        Status = rowStatus;
        ErrorMessage = NullIfEmpty(errorMessage);
        IsValid = DataEntryValues.RowStatuses.Importable.Contains(rowStatus);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public DataEntryBatch Batch { get; private set; } = null!;
    public int RowNumber { get; private set; }
    public string? CustomerCode { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string? NationalId { get; private set; }
    public string? MobileNumber { get; private set; }
    public string? Address { get; private set; }
    public string? Feedback { get; private set; }
    public string? Notes { get; private set; }
    public string? AccountNumber { get; private set; }
    public string? ContractNumber { get; private set; }
    public decimal? OutstandingBalance { get; private set; }
    public decimal? OverdueBalance { get; private set; }
    public int? DaysPastDue { get; private set; }
    public string Status { get; private set; } = DataEntryValues.RowStatuses.Ready;
    public string? ErrorMessage { get; private set; }
    public bool IsValid { get; private set; }
    public Guid? CollectionCustomerId { get; private set; }
    public CollectionCustomer? CollectionCustomer { get; private set; }
    public Guid? CollectionCaseId { get; private set; }
    public CollectionCase? CollectionCase { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer is required.", nameof(customerId));
        CollectionCustomerId = customerId;
    }

    public void LinkCase(Guid caseId)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("Case is required.", nameof(caseId));
        CollectionCaseId = caseId;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DataEntryNotification
{
    private DataEntryNotification() { }

    public DataEntryNotification(Guid batchId, Guid recipientUserId, string kind, string messageArabic, string messageEnglish, DateTimeOffset createdAt)
    {
        if (batchId == Guid.Empty || recipientUserId == Guid.Empty) throw new ArgumentException("Batch and recipient are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageArabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageEnglish);
        Id = Guid.NewGuid();
        BatchId = batchId;
        RecipientUserId = recipientUserId;
        Kind = kind.Trim().ToUpperInvariant();
        MessageArabic = messageArabic.Trim();
        MessageEnglish = messageEnglish.Trim();
        IsRead = false;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public DataEntryBatch Batch { get; private set; } = null!;
    public Guid RecipientUserId { get; private set; }
    public User RecipientUser { get; private set; } = null!;
    public string Kind { get; private set; } = string.Empty;
    public string MessageArabic { get; private set; } = string.Empty;
    public string MessageEnglish { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = now;
    }
}

public sealed class DataEntryDocument
{
    private DataEntryDocument() { }

    public DataEntryDocument(
        Guid customerId,
        Guid? batchId,
        Guid? caseId,
        string originalFileName,
        string contentType,
        long fileSize,
        string sha256Hash,
        string storageKey,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt,
        string? note = null)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer is required.", nameof(customerId));
        if (uploadedByUserId == Guid.Empty) throw new ArgumentException("Uploader is required.", nameof(uploadedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256Hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        BatchId = batchId;
        CaseId = caseId;
        OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim();
        FileSize = fileSize;
        Sha256Hash = sha256Hash.Trim();
        StorageKey = storageKey.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public CollectionCustomer Customer { get; private set; } = null!;
    public Guid? BatchId { get; private set; }
    public DataEntryBatch? Batch { get; private set; }
    public Guid? CaseId { get; private set; }
    public CollectionCase? Case { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string Sha256Hash { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public User UploadedByUser { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }

    public void AttachBatch(Guid batchId)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Batch is required.", nameof(batchId));
        BatchId ??= batchId;
    }

    public void AttachCase(Guid caseId)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("Case is required.", nameof(caseId));
        CaseId ??= caseId;
    }
}
