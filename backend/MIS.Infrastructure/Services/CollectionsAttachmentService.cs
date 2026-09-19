using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class CollectionsAttachmentService : ICollectionsAttachmentService
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private static readonly string[] Categories = ["CASE_DOCUMENT", "PAYMENT_PROOF", "VISIT_EVIDENCE", "COMPLAINT_DOCUMENT", "SETTLEMENT_DOCUMENT"];
    private readonly ApplicationDbContext _db; private readonly ICurrentUserContext _user; private readonly IHrFileStorage _files;
    public CollectionsAttachmentService(ApplicationDbContext db, ICurrentUserContext user, IHrFileStorage files) { _db = db; _user = user; _files = files; }

    public async Task<IReadOnlyCollection<CollectionAttachmentDto>> GetCaseAttachmentsAsync(Guid caseId, CancellationToken token)
    {
        await EnsureCaseAccessAsync(caseId, token); return await Project(_db.CollectionAttachments.AsNoTracking().Where(x => x.CaseId == caseId).OrderByDescending(x => x.UploadedAt)).ToArrayAsync(token);
    }

    public async Task<CollectionAttachmentDto> UploadAsync(Guid caseId, Guid? paymentId, string category, string fileName, string contentType, long length, Stream content, CancellationToken token)
    {
        EnsureWritePermission(); await EnsureCaseAccessAsync(caseId, token); var normalizedCategory = category.Trim().ToUpperInvariant(); if (!Categories.Contains(normalizedCategory)) throw new HrValidationException("Attachment category is invalid."); if (length <= 0 || length > MaximumBytes) throw new HrValidationException("Attachments must be between 1 byte and 10 MB."); if (paymentId.HasValue && !await _db.CollectionPayments.AnyAsync(x => x.Id == paymentId && x.CaseId == caseId, token)) throw new HrValidationException("Payment proof must reference a payment in the same case.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant(); var detectedType = await ValidateContentAsync(content, extension, token); if (normalizedCategory == "PAYMENT_PROOF" && !paymentId.HasValue) throw new HrValidationException("Payment proof requires a payment reference.");
        var stored = await _files.SaveAsync("collections-attachments", fileName, detectedType, content, MaximumBytes, token); var attachment = new CollectionAttachment(caseId, paymentId, normalizedCategory, stored.OriginalFileName, detectedType, stored.Length, stored.Sha256Hash, stored.StorageKey, _user.UserId, DateTimeOffset.UtcNow); _db.CollectionAttachments.Add(attachment); _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, "AttachmentUploaded", nameof(CollectionAttachment), attachment.Id, caseId, null, JsonSerializer.Serialize(new { attachment.Category, attachment.OriginalFileName, attachment.ContentType, attachment.FileSize }), "WEB", DateTimeOffset.UtcNow));
        try { await _db.SaveChangesAsync(token); } catch { await _files.DeleteAsync(stored.StorageKey, token); throw; } return await Project(_db.CollectionAttachments.AsNoTracking().Where(x => x.Id == attachment.Id)).SingleAsync(token);
    }

    public async Task<CollectionAttachmentDownloadDto> DownloadAsync(Guid attachmentId, CancellationToken token)
    {
        var attachment = await _db.CollectionAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId, token) ?? throw new HrNotFoundException("Attachment was not found."); await EnsureCaseAccessAsync(attachment.CaseId, token); var content = await _files.OpenReadAsync(attachment.StorageKey, token); _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, "AttachmentDownloaded", nameof(CollectionAttachment), attachment.Id, attachment.CaseId, null, JsonSerializer.Serialize(new { attachment.Category, attachment.OriginalFileName }), "WEB", DateTimeOffset.UtcNow)); await _db.SaveChangesAsync(token); return new CollectionAttachmentDownloadDto(content, attachment.ContentType, attachment.OriginalFileName);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken token)
    {
        var attachment = await _db.CollectionAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId, token) ?? throw new HrNotFoundException("Attachment was not found.");
        await EnsureCaseAccessAsync(attachment.CaseId, token);
        var isManager = HasRole(SystemRoleNames.Admin) || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsSupervisor);
        if (!isManager && attachment.UploadedById != _user.UserId) throw new HrForbiddenException("You can only remove attachments you uploaded.");
        _db.CollectionAttachments.Remove(attachment);
        _db.CollectionAuditLogs.Add(new CollectionAuditLog(_user.UserId, "AttachmentDeleted", nameof(CollectionAttachment), attachment.Id, attachment.CaseId, JsonSerializer.Serialize(new { attachment.Category, attachment.OriginalFileName, attachment.ContentType, attachment.FileSize }), null, "WEB", DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync(token);
        try { await _files.DeleteAsync(attachment.StorageKey, token); } catch { /* file already gone; audit + row removal is authoritative */ }
    }

    private async Task EnsureCaseAccessAsync(Guid caseId, CancellationToken token) { if (!await AccessibleCases().AnyAsync(x => x.Id == caseId, token)) throw new HrNotFoundException("Collection case was not found."); }
    private IQueryable<CollectionCase> AccessibleCases()
    {
        var userId = _user.UserId; if (HasRole(SystemRoleNames.Admin) || HasRole(SystemRoleNames.CollectionsOperationsManager) || HasRole(SystemRoleNames.CollectionsReviewer) || HasRole(SystemRoleNames.CollectionsAuditor)) return _db.CollectionCases; var supervisor = HasRole(SystemRoleNames.CollectionsSupervisor); var collector = HasRole(SystemRoleNames.CollectionsCollector); var viewer = HasRole(SystemRoleNames.CollectionsClientViewer);
        return _db.CollectionCases.Where(x => (collector && x.AssignedCollectorId == userId) || (supervisor && ((x.AssignedTeam != null && x.AssignedTeam.SupervisorId == userId) || (x.AssignedTeamId == null && _db.CollectionUserAccess.Any(a => a.UserId == userId && a.OrganizationId == x.Portfolio.OrganizationId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId))))) || (viewer && _db.CollectionUserAccess.Any(a => a.UserId == userId && a.OrganizationId == x.Portfolio.OrganizationId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId))));
    }
    private void EnsureWritePermission() { if (!HasRole(SystemRoleNames.Admin) && !HasRole(SystemRoleNames.CollectionsOperationsManager) && !HasRole(SystemRoleNames.CollectionsSupervisor) && !HasRole(SystemRoleNames.CollectionsCollector)) throw new HrForbiddenException("Your Collections role is read-only for attachments."); }
    private bool HasRole(string role) => _user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private static IQueryable<CollectionAttachmentDto> Project(IQueryable<CollectionAttachment> query) => query.Select(x => new CollectionAttachmentDto(x.Id, x.CaseId, x.PaymentId, x.Category, x.OriginalFileName, x.ContentType, x.FileSize, x.UploadedBy.FullName, x.UploadedAt));
    private static async Task<string> ValidateContentAsync(Stream stream, string extension, CancellationToken token)
    {
        if (!stream.CanSeek) throw new HrValidationException("Attachment stream must be seekable."); var header = new byte[8]; var read = await stream.ReadAsync(header, token); stream.Position = 0;
        var pdf = read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8); var jpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF; var png = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        return extension switch { ".pdf" when pdf => "application/pdf", ".jpg" or ".jpeg" when jpeg => "image/jpeg", ".png" when png => "image/png", _ => throw new HrValidationException("Only content-validated PDF, JPEG, and PNG attachments are accepted.") };
    }
}
