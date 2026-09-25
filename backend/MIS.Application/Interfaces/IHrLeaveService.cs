using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IHrLeaveService
{
    Task<PagedLeaveRequestsDto> GetPagedAsync(LeaveRequestFilterDto filter, CancellationToken cancellationToken);

    Task<LeaveRequestDetailsDto> GetDetailsAsync(Guid leaveRequestId, CancellationToken cancellationToken);

    Task<LeaveRequestDetailsDto> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken);

    Task<LeaveRequestDetailsDto> UpdateAsync(Guid leaveRequestId, UpdateLeaveRequest request, CancellationToken cancellationToken);

    Task<LeaveRequestDetailsDto> ApproveAsync(Guid leaveRequestId, ApproveLeaveRequest request, CancellationToken cancellationToken);

    Task<LeaveRequestDetailsDto> RejectAsync(Guid leaveRequestId, RejectLeaveRequest request, CancellationToken cancellationToken);

    Task<LeaveRequestDetailsDto> CancelAsync(Guid leaveRequestId, CancelLeaveRequest request, CancellationToken cancellationToken);

    Task<PagedLeaveBalancesDto> GetBalancesAsync(LeaveBalanceFilterDto filter, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LeaveBalanceDto>> GetEmployeeBalancesAsync(Guid employeeId, int year, CancellationToken cancellationToken);

    Task<LeaveEntitlementDto> UpsertEntitlementAsync(
        Guid employeeId,
        Guid leaveTypeId,
        int year,
        UpsertLeaveEntitlementRequest request,
        CancellationToken cancellationToken);

    Task<LeaveImportReviewDto> ReviewImportAsync(Stream stream, string fileName, long length, CancellationToken cancellationToken);
    Task<LeaveImportResultDto> ConfirmImportAsync(Guid importId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null);
    Task<LeaveTemplateDto> BuildImportTemplateAsync(CancellationToken cancellationToken);
}
