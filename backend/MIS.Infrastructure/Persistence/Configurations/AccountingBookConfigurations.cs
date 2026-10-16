using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

public sealed class AccountingBookEntryConfiguration : IEntityTypeConfiguration<AccountingBookEntry>
{
    public void Configure(EntityTypeBuilder<AccountingBookEntry> b)
    {
        b.ToTable("AccountingBookEntries", table => table.HasCheckConstraint("CK_AccountingBookEntries_Amount", "\"Amount\" > 0"));
        b.HasKey(x => x.Id); b.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired(); b.Property(x => x.Category).HasMaxLength(40).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.TransactionDate).HasColumnType("date"); b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(160); b.Property(x => x.SourceKey).HasMaxLength(150);
        b.HasIndex(x => x.DocumentNumber).IsUnique(); b.HasIndex(x => x.SourceKey).IsUnique(); b.HasIndex(x => new { x.Category, x.TransactionDate }); b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.JournalEntryId).IsUnique();
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CounterAccount).WithMany().HasForeignKey(x => x.CounterAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.JournalEntry).WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RelatedAdvance).WithMany().HasForeignKey(x => x.RelatedAdvanceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RecurringExpense).WithMany().HasForeignKey(x => x.RecurringExpenseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DeletedById).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AccountingRecurringExpenseConfiguration : IEntityTypeConfiguration<AccountingRecurringExpense>
{
    public void Configure(EntityTypeBuilder<AccountingRecurringExpense> b)
    {
        b.ToTable("AccountingRecurringExpenses"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CashAccount).WithMany().HasForeignKey(x => x.CashAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
