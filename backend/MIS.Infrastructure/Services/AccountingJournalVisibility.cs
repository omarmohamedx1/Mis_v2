using Microsoft.EntityFrameworkCore;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

internal static class AccountingJournalVisibility
{
    // Keep the ledger and audit history intact while excluding deleted operations
    // and their balancing corrections from the operational journal register.
    internal static IQueryable<JournalEntry> Visible(ApplicationDbContext db) => db.JournalEntries.AsNoTracking()
        .Where(j => j.EntryType != "ACCOUNTING_DELETE"
            && !db.AccountingBookEntries.Any(e => e.DeletedAt != null
                && (e.JournalEntryId == j.Id || e.JournalEntryId == j.ReversalOfJournalId)));
}
