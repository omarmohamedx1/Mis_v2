using MIS.Application.DTOs.Finance;
using MIS.Domain.Entities;

namespace MIS.Application.Interfaces;

public interface IFinanceService
{
    Task<FinanceDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<IReadOnlyCollection<FinanceAccountDto>> GetAccountsAsync(CancellationToken token);
    Task<IReadOnlyCollection<AccountingPeriodDto>> GetPeriodsAsync(int? year, CancellationToken token);
    Task<IReadOnlyCollection<AccountingPeriodDto>> InitializeYearAsync(int year, CancellationToken token);
    Task<AccountingPeriodDto> ChangePeriodStatusAsync(Guid id, string action, PeriodActionRequest request, CancellationToken token);
    Task<FinancePagedResultDto<FinanceJournalListItemDto>> GetJournalsAsync(int page, int pageSize, string? status, DateOnly? from, DateOnly? to, CancellationToken token);
    Task<JournalDto> GetJournalAsync(Guid id, CancellationToken token);
    Task<JournalDto> CreateManualJournalAsync(CreateManualJournalRequest request, CancellationToken token);
    Task<JournalDto> SubmitJournalAsync(Guid id, CancellationToken token);
    Task<JournalDto> ApproveJournalAsync(Guid id, CancellationToken token);
    Task<JournalDto> PostJournalAsync(Guid id, CancellationToken token);
    Task<JournalDto> ReverseJournalAsync(Guid id, PeriodActionRequest request, CancellationToken token);
    Task<ClientLedgerDto> GetClientLedgerAsync(Guid clientId, DateOnly? from, DateOnly? to, CancellationToken token);
    Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOf, CancellationToken token);
    Task<FinancePagedResultDto<FinancialAuditDto>> GetAuditAsync(int page, int pageSize, CancellationToken token);
    Task<FinancePagedResultDto<CollectionFinanceListItemDto>> GetFinancialCollectionsAsync(int page, int pageSize, string? status, string? channel, CancellationToken token);
    Task<CollectionFinanceDto> GetFinancialCollectionAsync(Guid paymentId, CancellationToken token);
    Task<CollectionFinanceDto> ClearCollectionAsync(Guid paymentId, ClearCollectionRequest request, CancellationToken token);
    Task<CollectionFinanceDto> ReverseCollectionAsync(Guid paymentId, PeriodActionRequest request, CancellationToken token);
    Task<IReadOnlyCollection<CustodySummaryDto>> GetCustodiesAsync(CancellationToken token);
    Task<CustodyDetailsDto> GetCustodyAsync(Guid collectorId, CancellationToken token);
    Task<CustodyDetailsDto> UpdateCustodyLimitsAsync(Guid collectorId, UpdateCustodyLimitsRequest request, CancellationToken token);
}

public interface IFinancePostingService
{
    Task<JournalEntry> PostApprovedCollectionAsync(CollectionPayment payment, CollectionCase collectionCase, CancellationToken token);
}
