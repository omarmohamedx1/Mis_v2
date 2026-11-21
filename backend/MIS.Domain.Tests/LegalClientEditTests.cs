using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientEditTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_details_and_update_use_customer_id_for_assigned_and_transferred_clients(bool transferred)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"legal-edit-{Guid.NewGuid()}").Options);
        var now = DateTimeOffset.UtcNow;
        var department = new Department("Legal", "LEGAL", now);
        var user = new User("legal", "legal@example.invalid", "hash", "Legal user", department.Id, now);
        var bank = new ClientOrganization("TEST", "بنك", "Bank", "BANK", now);
        var first = new CollectionCustomer(bank.Id, "C-1", "أول", "First", now);
        var selected = new CollectionCustomer(bank.Id, "C-2", "ثاني", "Second", now);
        var firstAssignment = new LegalClientAssignment(first.Id, user.Id, now);
        firstAssignment.Update("ACTIVE", "First notes", user.Id, now);
        db.AddRange(department, user, bank, first, selected, firstAssignment);
        if (transferred)
        {
            var portfolio = new CollectionPortfolio(bank.Id, "P", "محفظة", "Portfolio", "EGP", now);
            var linkedCase = new CollectionCase(portfolio.Id, selected.Id, "CASE-2", "ACCOUNT-2", 100, 80, 20, 10, Guid.NewGuid(), now);
            linkedCase.UpdatePortfolioCase("LEGAL", null, now);
            db.AddRange(portfolio, linkedCase);
        }
        else
        {
            var assignment = new LegalClientAssignment(selected.Id, user.Id, now);
            assignment.Update("ON_HOLD", "Second notes", user.Id, now);
            db.Add(assignment);
        }
        await db.SaveChangesAsync();
        var service = new LegalClientService(db, new Principal(user.Id, true), null!);
        var row = (await service.ListAsync("C-2", 1, 20, default)).Items.Single();
        Assert.Equal(selected.Id, row.CustomerId);
        var loaded = await service.GetAsync(row.CustomerId, default);
        Assert.Equal(row.CustomerId, loaded.Client.CustomerId);
        Assert.True(loaded.CanManage);
        Assert.Equal(transferred ? null : "Second notes", loaded.Notes);
        await service.UpdateAsync(row.CustomerId, new UpdateLegalClientRequest("ON_HOLD", "Updated second notes"), default);
        Assert.Equal("Updated second notes", (await service.GetAsync(row.CustomerId, default)).Notes);
        Assert.Equal("First notes", (await service.GetAsync(first.Id, default)).Notes);
        Assert.Equal("Second", (await db.CollectionCustomers.SingleAsync(x => x.Id == selected.Id)).FullNameEnglish);
        var denied = new LegalClientService(db, new Principal(user.Id, false), null!);
        Assert.False((await denied.GetAsync(row.CustomerId, default)).CanManage);
        await Assert.ThrowsAsync<HrForbiddenException>(() => denied.UpdateAsync(row.CustomerId, new UpdateLegalClientRequest("ACTIVE", "Denied"), default));
        await Assert.ThrowsAsync<HrNotFoundException>(() => service.GetAsync(Guid.NewGuid(), default));
        // Assignment IDs are deliberately not accepted as customer IDs.
        await Assert.ThrowsAsync<HrNotFoundException>(() => service.GetAsync(firstAssignment.Id, default));
    }

    private sealed class Principal(Guid id, bool manage) : ICurrentUserContext
    {
        public Guid UserId => id;
        public string Username => "legal-test";
        public IReadOnlyCollection<string> Roles => manage ? ["LegalOfficer"] : [];
        public IReadOnlyCollection<string> Permissions => [];
    }
}
