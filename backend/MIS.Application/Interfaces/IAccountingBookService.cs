using MIS.Application.DTOs.Accounting;

namespace MIS.Application.Interfaces;

public interface IAccountingBookService
{
    Task<AccountingBookPageDto> ListAsync(AccountingBookFilter filter, CancellationToken token);
    Task<AccountingBookEntryDto> CreateAsync(CreateAccountingBookEntryRequest request, CancellationToken token);
    Task<AccountingBookEntryDto> TransitionAsync(Guid id, string action, string? reason, CancellationToken token);
    Task DeleteAsync(Guid id, ConfirmAccountingDeletionRequest request, CancellationToken token);
    Task DeleteSourceAsync(string kind, Guid id, ConfirmAccountingDeletionRequest request, CancellationToken token);
    Task<AccountingBookSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken token);
    Task<IReadOnlyList<AccountingAdvanceDto>> AdvancesAsync(DateOnly asOf, string? search, Guid? employeeId, CancellationToken token);
    Task<IReadOnlyList<AccountingRecurringExpenseDto>> RecurringAsync(CancellationToken token);
    Task<AccountingRecurringExpenseDto> SaveRecurringAsync(Guid? id, SaveRecurringExpenseRequest request, CancellationToken token);
    Task<GenerateRecurringExpensesResult> GenerateRecurringAsync(GenerateRecurringExpensesRequest request, CancellationToken token);
    Task RecordPaidSourceAsync(AccountingPaidSource source, CancellationToken token);
    Task<AccountingCommissionPreviewDto> PreviewCommissionsAsync(int year, int month, CancellationToken token);
    Task<byte[]> ExportAsync(string report, AccountingExportFilter filter, CancellationToken token);
}
