using MIS.Application.DTOs.Accounting;
using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IAccountingService
{
    Task<AccountingDashboardDto> GetDashboardAsync(int year, int month, CancellationToken token);
    Task<IReadOnlyList<AccountingPayrollPeriodDto>> ListPeriodsAsync(CancellationToken token);
    Task<AccountingPayrollPeriodDto> EnsurePeriodAsync(GeneratePayrollRequest request, CancellationToken token);
    Task<AccountingPayrollPeriodDto> GeneratePayrollAsync(GeneratePayrollRequest request, CancellationToken token);
    Task<AccountingPayrollPageDto> ListPayrollsAsync(Guid periodId, string? search, string? status, int page, int pageSize, CancellationToken token);
    Task<AccountingEmployeePayrollDto> GetPayrollAsync(Guid id, CancellationToken token);
    Task<AccountingEmployeePayrollDto> UpdatePayrollAsync(Guid id, UpdateAccountingPayrollRequest request, CancellationToken token);
    Task<AccountingEmployeePayrollDto> SubmitPayrollAsync(Guid id, CancellationToken token);
    Task<AccountingEmployeePayrollDto> ApprovePayrollAsync(Guid id, CancellationToken token);
    Task<AccountingEmployeePayrollDto> PayPayrollAsync(Guid id, CancellationToken token);
    Task<AccountingEmployeePayrollDto> CancelPayrollAsync(Guid id, CancellationToken token);

    Task<AccountingTransportationPageDto> ListTransportationAsync(string? search, string? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken token);
    Task<AccountingTransportationDto> GetTransportationAsync(Guid id, CancellationToken token);
    Task<AccountingTransportationDto> CreateTransportationAsync(CreateTransportationRequest request, CancellationToken token);
    Task<AccountingTransportationDto> UpdateTransportationAsync(Guid id, UpdateTransportationRequest request, CancellationToken token);
    Task<AccountingTransportationDto> TransitionTransportationAsync(Guid id, string action, CancellationToken token);
    Task<AccountingTransportationDto> UploadTransportationAttachmentAsync(Guid id, HrUploadFile file, CancellationToken token);
    Task DeleteTransportationAttachmentAsync(Guid id, CancellationToken token);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadTransportationAttachmentAsync(Guid id, CancellationToken token);

    Task<IReadOnlyList<AccountingCommissionRuleDto>> ListRulesAsync(string? scope, CancellationToken token);
    Task<AccountingCommissionRuleDto> CreateRuleAsync(CreateCommissionRuleRequest request, CancellationToken token);
    Task<AccountingCommissionRuleDto> SetRuleActiveAsync(Guid id, bool isActive, CancellationToken token);
    Task DeleteRuleAsync(Guid id, CancellationToken token);

    Task<int> CalculateCollectorCommissionsAsync(CalculateCommissionsRequest request, CancellationToken token);
    Task<AccountingCommissionPageDto<AccountingCollectorCommissionDto>> ListCollectorCommissionsAsync(int? year, int? month, string? search, string? status, int page, int pageSize, CancellationToken token);
    Task<AccountingCollectorCommissionDetailsDto> GetCollectorCommissionAsync(Guid id, CancellationToken token);
    Task<AccountingCollectorCommissionDto> AdjustCollectorCommissionAsync(Guid id, AdjustCommissionRequest request, CancellationToken token);
    Task<AccountingCollectorCommissionDto> TransitionCollectorCommissionAsync(Guid id, string action, CancellationToken token);

    Task<int> CalculateSupervisorCommissionsAsync(CalculateCommissionsRequest request, CancellationToken token);
    Task<AccountingCommissionPageDto<AccountingSupervisorCommissionDto>> ListSupervisorCommissionsAsync(int? year, int? month, string? search, string? status, int page, int pageSize, CancellationToken token);
    Task<AccountingSupervisorCommissionDto> GetSupervisorCommissionAsync(Guid id, CancellationToken token);
    Task<AccountingSupervisorCommissionDto> AdjustSupervisorCommissionAsync(Guid id, AdjustCommissionRequest request, CancellationToken token);
    Task<AccountingSupervisorCommissionDto> TransitionSupervisorCommissionAsync(Guid id, string action, CancellationToken token);

    Task<IReadOnlyList<AccountingLookupEmployeeDto>> LookupEmployeesAsync(string? search, CancellationToken token);
}
