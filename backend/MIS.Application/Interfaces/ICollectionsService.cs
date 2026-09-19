using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface ICollectionsService
{
    Task<CollectionDashboardDto> GetDashboardAsync(Guid? organizationId, CancellationToken cancellationToken);
    Task<CollectionCreditorDashboardDto> GetCreditorDashboardAsync(CancellationToken cancellationToken);
    Task<CollectionCollectorDashboardDto> GetCollectorDashboardAsync(CancellationToken cancellationToken);
    Task<PagedResultDto<ClientOrganizationCardDto>> GetClientsAsync(int page, int pageSize, string? search, string? type, bool? active, CancellationToken cancellationToken);
    Task<PagedResultDto<CollectionCaseListItemDto>> GetCasesAsync(CollectionFilters filters, CancellationToken cancellationToken);
    Task<CollectionCaseDetailsDto> GetCaseAsync(Guid caseId, bool revealSensitive, CancellationToken cancellationToken);
    Task<NationalIdLookupDto> LookupByNationalIdAsync(string nationalId, CancellationToken cancellationToken);
    Task<WorkQueueDto> GetMyWorkAsync(CancellationToken cancellationToken);
    Task<CollectionActivityDto> CreateActivityAsync(Guid caseId, CreateActivityRequest request, CancellationToken cancellationToken);
    Task<PromiseToPayDto> CreatePromiseAsync(Guid caseId, CreatePromiseRequest request, CancellationToken cancellationToken);
    Task<PagedResultDto<PromiseToPayDto>> GetPromisesAsync(PromiseFilters filters, CancellationToken cancellationToken);
    Task<CollectionPaymentDto> SubmitPaymentAsync(Guid caseId, SubmitPaymentRequest request, CancellationToken cancellationToken);
    Task<CollectionPaymentDto> ReviewPaymentAsync(Guid paymentId, ReviewPaymentRequest request, CancellationToken cancellationToken);
    Task<PagedResultDto<CollectionPaymentDto>> GetPaymentsAsync(PaymentFilters filters, CancellationToken cancellationToken);
    Task<CollectionPaymentSummaryDto> GetPaymentSummaryAsync(CancellationToken cancellationToken);
    Task<CollectionPaymentFilterOptionsDto> GetPaymentFilterOptionsAsync(CancellationToken cancellationToken);
    Task<CollectionPaymentDetailsDto> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken);
    Task<AssignmentPreviewDto> PreviewAssignmentAsync(IReadOnlyCollection<Guid> caseIds, Guid collectorId, CancellationToken cancellationToken);
    Task<AssignmentPreviewDto> AssignCasesAsync(BulkAssignmentRequest request, CancellationToken cancellationToken);
    Task<ImportedCollectorLinkResultDto> PreviewImportedCollectorLinksAsync(Guid? organizationId, CancellationToken cancellationToken);
    Task<ImportedCollectorLinkResultDto> ApplyImportedCollectorLinksAsync(Guid? organizationId, CancellationToken cancellationToken);
    Task<AssignmentPreviewDto> PreviewUnassignmentAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken);
    Task<AssignmentPreviewDto> UnassignCasesAsync(BulkUnassignRequest request, CancellationToken cancellationToken);
    Task<AutoAssignmentPreviewDto> PreviewAutomaticAssignmentAsync(AutoAssignmentRequest request, CancellationToken cancellationToken);
    Task<AutoAssignmentPreviewDto> ApplyAutomaticAssignmentAsync(AutoAssignmentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CollectorLookupDto>> GetCollectorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CollectorLookupDto>> GetCaseFilterCollectorsAsync(CancellationToken cancellationToken);
    Task<PagedResultDto<FieldVisitDto>> GetVisitsAsync(VisitFilters filters, CancellationToken cancellationToken);
    Task<FieldVisitSummaryDto> GetVisitSummaryAsync(CancellationToken cancellationToken);
    Task<FieldVisitFilterOptionsDto> GetVisitFilterOptionsAsync(CancellationToken cancellationToken);
    Task<FieldVisitScheduleOptionsDto> GetVisitScheduleOptionsAsync(string? search, CancellationToken cancellationToken);
    Task<FieldVisitDetailsDto> GetVisitAsync(Guid visitId, CancellationToken cancellationToken);
    Task<FieldVisitDto> CreateVisitAsync(CreateVisitRequest request, CancellationToken cancellationToken);
    Task<FieldVisitDto> CompleteVisitAsync(Guid visitId, CompleteVisitRequest request, CancellationToken cancellationToken);
    Task<PagedResultDto<ComplaintDto>> GetComplaintsAsync(int page, int pageSize, string? search, string? status, CancellationToken cancellationToken);
    Task<ComplaintDto> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken cancellationToken);
    Task<ComplaintDto> ChangeComplaintStatusAsync(Guid complaintId, ChangeComplaintStatusRequest request, CancellationToken cancellationToken);
    Task<PagedResultDto<CollectionAuditDto>> GetAuditAsync(int page, int pageSize, string? search, Guid? caseId, CancellationToken cancellationToken);
    Task<CollectionsConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<ClientConfigurationDto> SaveClientAsync(Guid? id, SaveClientConfigurationRequest request, CancellationToken cancellationToken);
    Task<PortfolioConfigurationDto> SavePortfolioAsync(Guid? id, SavePortfolioConfigurationRequest request, CancellationToken cancellationToken);
    Task<PortfolioConfigurationDto> EnsureDeskAsync(EnsureCollectionDeskRequest request, CancellationToken cancellationToken);
    Task<BucketConfigurationDto> SaveBucketAsync(Guid? id, SaveBucketConfigurationRequest request, CancellationToken cancellationToken);
    Task DeleteClientAsync(Guid id, CancellationToken cancellationToken);
    Task DeletePortfolioAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteBucketAsync(Guid id, CancellationToken cancellationToken);
    Task ArchiveCaseAsync(Guid caseId, ArchiveCaseRequest request, CancellationToken cancellationToken);
    Task RestoreCaseAsync(Guid caseId, RestoreCaseRequest request, CancellationToken cancellationToken);
    Task<PromiseToPayDto> ChangePromiseStatusAsync(Guid promiseId, ChangePromiseStatusRequest request, CancellationToken cancellationToken);
}
