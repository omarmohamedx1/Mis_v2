using System.Text.Json.Serialization;

namespace MIS.Application.DTOs.Collections;

public sealed record BankTargetRow([property: JsonRequired] int Bucket, [property: JsonRequired] string Movement, [property: JsonRequired] decimal TargetPercent, [property: JsonRequired] decimal Weight);
public sealed record BankTargetValues([property: JsonRequired] IReadOnlyList<BankTargetRow> Rows, [property: JsonRequired] decimal MoreTargetPercent,
    [property: JsonRequired] decimal MoreWeight, [property: JsonRequired] decimal ActiveClassWeight, [property: JsonRequired] decimal MoreClassWeight, [property: JsonRequired] decimal WOffBelow90, [property: JsonRequired] decimal WOffAbove90);
public sealed record SaveBankTargetsRequest(Guid? ExpectedVersion, BankTargetValues Values);
public sealed record BankTargetVersionRequest(Guid ExpectedVersion);
public sealed record BankTargetAuditDto(Guid Id, string Action, string Status, Guid ActorId,
    DateTimeOffset OccurredAt, Guid Version, BankTargetValues Values);
public sealed record BankTargetConfigurationDto(Guid Id, string Status, Guid Version, BankTargetValues Values,
    Guid CreatedBy, DateTimeOffset CreatedAt, Guid UpdatedBy, DateTimeOffset UpdatedAt,
    Guid? ReviewedBy, DateTimeOffset? ReviewedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, Guid? CopiedFromId);
public sealed record BankTargetScoresDto(decimal? Active, decimal? More, string Status, IReadOnlyList<string> Issues);
public sealed record BankTargetsDto(BankTargetConfigurationDto? Configuration, bool CanEdit, bool CanApprove,
    BankTargetScoresDto Scores, IReadOnlyList<BankTargetAuditDto> History);
