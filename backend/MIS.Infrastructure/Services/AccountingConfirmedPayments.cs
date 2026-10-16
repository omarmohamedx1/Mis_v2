using System.Data;
using MIS.Application.DTOs.Accounting;
using Microsoft.EntityFrameworkCore;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingService
{
    public async Task<AccountingEmployeePayrollDto> RecordPayrollAsync(Guid id, UpdateAccountingPayrollRequest request, CancellationToken token)
    {
        EnsureManage();
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        await UpdatePayrollAsync(id, request, token);
        var result = await PayPayrollAsync(id, token);
        if (tx is not null) await tx.CommitAsync(token);
        return result;
    }
    public async Task<AccountingCollectorCommissionDto> RecordCollectorCommissionAsync(Guid id, AdjustCommissionRequest request, CancellationToken token)
    {
        EnsureManage();
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        await AdjustCollectorCommissionAsync(id, request, token);
        var result = await TransitionCollectorCommissionAsync(id, "pay", token);
        if (tx is not null) await tx.CommitAsync(token);
        return result;
    }
    public async Task<AccountingSupervisorCommissionDto> RecordSupervisorCommissionAsync(Guid id, AdjustCommissionRequest request, CancellationToken token)
    {
        EnsureManage();
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        await AdjustSupervisorCommissionAsync(id, request, token);
        var result = await TransitionSupervisorCommissionAsync(id, "pay", token);
        if (tx is not null) await tx.CommitAsync(token);
        return result;
    }
}
