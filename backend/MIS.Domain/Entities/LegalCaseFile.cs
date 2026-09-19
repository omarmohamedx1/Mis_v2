using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class LegalCaseFile
{
    private LegalCaseFile() { }

    public LegalCaseFile(Guid collectionCaseId, Guid receivedByUserId, DateTimeOffset receivedAt)
    {
        if (collectionCaseId == Guid.Empty) throw new ArgumentException("Collection case is required.", nameof(collectionCaseId));
        if (receivedByUserId == Guid.Empty) throw new ArgumentException("Receiver is required.", nameof(receivedByUserId));
        Id = Guid.NewGuid();
        CollectionCaseId = collectionCaseId;
        Stage = LegalValues.Stages.Intake;
        ReceivedByUserId = receivedByUserId;
        ReceivedAt = receivedAt;
        UpdatedAt = receivedAt;
    }

    public Guid Id { get; private set; }
    public Guid CollectionCaseId { get; private set; }
    public CollectionCase CollectionCase { get; private set; } = null!;
    public string Stage { get; private set; } = LegalValues.Stages.Intake;
    public string? CourtName { get; private set; }
    public string? CourtCaseNumber { get; private set; }
    public string? LawyerName { get; private set; }
    public DateOnly? NextHearingOn { get; private set; }
    public string? Notes { get; private set; }
    public Guid ReceivedByUserId { get; private set; }
    public User ReceivedByUser { get; private set; } = null!;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<LegalCaseAction> Actions { get; private set; } = new List<LegalCaseAction>();

    public void UpdateFile(string? courtName, string? courtCaseNumber, string? lawyerName, DateOnly? nextHearingOn, string? notes, DateTimeOffset updatedAt)
    {
        CourtName = Normalize(courtName, 160);
        CourtCaseNumber = Normalize(courtCaseNumber, 80);
        LawyerName = Normalize(lawyerName, 160);
        NextHearingOn = nextHearingOn;
        Notes = Normalize(notes, 2000);
        UpdatedAt = updatedAt;
    }

    public void SetStage(string stage, DateTimeOffset updatedAt)
    {
        if (!LegalValues.Stages.IsValid(stage)) throw new ArgumentException("Invalid legal stage.", nameof(stage));
        Stage = stage.Trim().ToUpperInvariant();
        UpdatedAt = updatedAt;
    }

    private static string? Normalize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}

public sealed class LegalCaseAction
{
    private LegalCaseAction() { }

    public LegalCaseAction(Guid fileId, string actionType, string? result, string notes, DateOnly? happenedOn, Guid createdByUserId, DateTimeOffset createdAt)
    {
        if (fileId == Guid.Empty) throw new ArgumentException("Legal file is required.", nameof(fileId));
        if (!LegalValues.Actions.IsValid(actionType)) throw new ArgumentException("Invalid legal action.", nameof(actionType));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);
        Id = Guid.NewGuid();
        FileId = fileId;
        ActionType = actionType.Trim().ToUpperInvariant();
        Result = string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        Notes = notes.Trim();
        HappenedOn = happenedOn;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FileId { get; private set; }
    public LegalCaseFile File { get; private set; } = null!;
    public string ActionType { get; private set; } = string.Empty;
    public string? Result { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public DateOnly? HappenedOn { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public User CreatedByUser { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
}
