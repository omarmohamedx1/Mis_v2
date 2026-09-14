namespace MIS.Domain.Entities;

public sealed class HrExcuseAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExcuseId { get; set; }
    public HrExcuseMission Excuse { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public long Length { get; set; }
    public string Sha256Hash { get; set; } = "";
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
