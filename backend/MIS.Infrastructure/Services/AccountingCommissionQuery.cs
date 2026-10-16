using Microsoft.EntityFrameworkCore;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

internal sealed record CommissionPayment(Guid Id, Guid CaseId, string CaseNumber, DateOnly PaymentDate, decimal Amount, string ReferenceNumber, Guid CollectorId);
internal static class AccountingCommissionQuery
{
    public static Task<List<CommissionPayment>> Payments(ApplicationDbContext db, DateOnly from, DateOnly to, CancellationToken token) => db.CollectionPayments.AsNoTracking()
        .Where(x => x.Status == "APPROVED" && x.PaymentDate >= from && x.PaymentDate <= to)
        .Select(x => new CommissionPayment(x.Id, x.CaseId, x.Case.CaseNumber, x.PaymentDate, x.Amount, x.ReferenceNumber,
            db.CollectionFinancialReceipts.Where(r => r.CollectionPaymentId == x.Id).Select(r => r.CollectorId).FirstOrDefault() ?? x.Case.AssignedCollectorId ?? x.SubmittedById)).ToListAsync(token);
}
