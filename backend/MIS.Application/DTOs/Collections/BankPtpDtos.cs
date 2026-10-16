using System.ComponentModel.DataAnnotations;

namespace MIS.Application.DTOs.Collections;

public sealed record BankPtpQuery(int Page = 1, int PageSize = 20, string? Search = null, string? View = null,
    string? Status = null, DateOnly? PromiseDate = null, Guid? CollectorId = null, string? SortBy = null, string? SortDirection = null);
public sealed record BankPtpAccessDto(bool IsManager, bool CanCreate, bool CanChangeStatus);
public sealed record BankPtpSummaryDto(int DueToday, int Upcoming, int Overdue, int Broken);
public sealed record BankPtpItemDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, decimal PromiseAmount,
    DateOnly PromiseDate, string Status, string OperationalState, Guid CollectorId, string CollectorName,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record BankPtpPageDto(IReadOnlyCollection<BankPtpItemDto> Items, int TotalCount, int Page, int PageSize,
    int TotalPages, BankPtpAccessDto Access);
public sealed record BankPtpDetailsDto(Guid Id, Guid CaseId, string CaseNumber, string CustomerName, string? Mobile,
    decimal OutstandingAmount, string BankName, decimal PromiseAmount, DateOnly PromiseDate, string Status,
    string OperationalState, Guid CollectorId, string CollectorName, string? Notes, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, BankPtpAccessDto Access);
public sealed record CreateBankPtpRequest(Guid CaseId,
    [Range(typeof(decimal), "0.01", "9999999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal PromiseAmount, DateOnly PromiseDate,
    [MaxLength(2000)] string? Notes);
public sealed record ChangeBankPtpStatusRequest([Required, MaxLength(32)] string Status);
