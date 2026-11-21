using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientArchiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Archive_hides_only_selected_client_and_preserves_linked_history(bool hasAssignment)
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var department = new Department("Legal", "LEGAL", now);
        var user = new User("legal", "legal@example.invalid", "hash", "Legal user", department.Id, now);
        var bank = new ClientOrganization("TEST", "بنك", "Bank", "BANK", now);
        var customer = new CollectionCustomer(bank.Id, "C-1", "عميل", "Client", now);
        var other = new CollectionCustomer(bank.Id, "C-2", "آخر", "Other", now);
        var portfolio = new CollectionPortfolio(bank.Id, "P", "محفظة", "Portfolio", "EGP", now);
        var linkedCase = new CollectionCase(portfolio.Id, customer.Id, "CASE-1", "ACCOUNT-1", 100, 80, 20, 10, Guid.NewGuid(), now);
        var legalFile = new LegalCaseFile(linkedCase.Id, user.Id, now);
        var payment = new CollectionPayment(linkedCase.Id, 20, DateOnly.FromDateTime(now.UtcDateTime), "CASH", "REF", user.Id, "proof", now);
        payment.Review(user.Id, true, null, false, now);
        var journalId = Guid.NewGuid();
        payment.MarkFinanciallyPosted(journalId);
        var document = new LegalClientDocument(customer.Id, "document.pdf", "application/pdf", 100, "hash", "stored-file", user.Id, now);
        db.AddRange(department, user, bank, customer, other, portfolio, linkedCase, legalFile, payment, document,
            new LegalClientAssignment(other.Id, user.Id, now));
        if (hasAssignment)
        {
            var assignment = new LegalClientAssignment(customer.Id, user.Id, now);
            assignment.Update("ON_HOLD", "Keep legal notes", user.Id, now);
            assignment.SetImportedData("external", "[{\"value\":\"original\"}]");
            db.Add(assignment);
        }
        await db.SaveChangesAsync();
        var service = new LegalClientService(db, new Principal(user.Id, true), null!);
        Assert.Equal(2, (await service.ListAsync(null, 1, 20, default)).TotalCount);

        await service.ArchiveAsync(customer.Id, default);
        await service.ArchiveAsync(customer.Id, default); // Repeated confirmation is harmless.
        db.ChangeTracker.Clear();

        var list = await service.ListAsync(null, 1, 20, default);
        Assert.Equal(other.Id, Assert.Single(list.Items).CustomerId);
        Assert.Empty((await service.ListAsync("C-1", 1, 20, default)).Items);
        Assert.Equal(1, (await service.GetDashboardAsync(default)).TotalClients);
        var archived = await service.GetAsync(customer.Id, default);
        Assert.Equal("ARCHIVED", archived.Client.Status);
        Assert.False(archived.CanManage);
        Assert.Single(archived.Documents);
        Assert.Contains("CASE-1", archived.CollectionCaseNumbers);
        Assert.Equal(2, await db.CollectionCustomers.CountAsync());
        Assert.Equal(legalFile.Id, (await db.LegalCaseFiles.SingleAsync()).Id);
        var preservedCase = await db.CollectionCases.SingleAsync();
        Assert.False(preservedCase.IsArchived);
        Assert.Equal(80, preservedCase.OutstandingBalance);
        var preservedPayment = await db.CollectionPayments.SingleAsync();
        Assert.Equal(20, preservedPayment.Amount);
        Assert.Equal(journalId, preservedPayment.FinancialJournalEntryId);
        Assert.Equal("stored-file", (await db.LegalClientDocuments.SingleAsync()).StorageKey);
        Assert.Single(await db.CollectionAuditLogs.Where(x => x.Action == "LegalClientArchived").ToArrayAsync());
        var storedAssignment = await db.LegalClientAssignments.SingleAsync(x => x.CustomerId == customer.Id);
        if (hasAssignment)
        {
            Assert.Equal("Keep legal notes", storedAssignment.Notes);
            Assert.Equal("external", storedAssignment.ExternalIdentifier);
            Assert.Equal("[{\"value\":\"original\"}]", storedAssignment.ImportedFieldsJson);
        }
        await Assert.ThrowsAsync<HrValidationException>(() => service.UpdateAsync(customer.Id, new UpdateLegalClientRequest("ACTIVE", "stale edit"), default));
    }

    [Fact]
    public async Task Archive_requires_manage_permission_and_rejects_non_legal_clients()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var denied = new LegalClientService(db, new Principal(userId, false), null!);
        await Assert.ThrowsAsync<HrForbiddenException>(() => denied.ArchiveAsync(Guid.NewGuid(), default));
        var allowed = new LegalClientService(db, new Principal(userId, true), null!);
        await Assert.ThrowsAsync<HrNotFoundException>(() => allowed.ArchiveAsync(Guid.NewGuid(), default));
        Assert.Empty(await db.LegalClientAssignments.ToArrayAsync());
        Assert.Empty(await db.CollectionAuditLogs.ToArrayAsync());
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"legal-archive-{Guid.NewGuid()}").Options);

    private sealed class Principal(Guid id, bool manage) : ICurrentUserContext
    {
        public Guid UserId => id;
        public string Username => "legal-test";
        public IReadOnlyCollection<string> Roles => manage ? ["LegalOfficer"] : [];
        public IReadOnlyCollection<string> Permissions => [];
    }
}
