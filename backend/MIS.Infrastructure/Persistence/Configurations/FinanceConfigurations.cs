using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Configurations;

internal static class FinanceSeed
{
    public static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid CashboxAccountId = Guid.Parse("10000000-0000-0000-0001-000000110100");
    public static readonly Guid BankAccountId = Guid.Parse("10000000-0000-0000-0001-000000110200");
    public static readonly Guid CustodyAccountId = Guid.Parse("10000000-0000-0000-0001-000000111100");
    public static readonly Guid BankClearingAccountId = Guid.Parse("10000000-0000-0000-0001-000000112100");
    public static readonly Guid ChequesAccountId = Guid.Parse("10000000-0000-0000-0001-000000112200");
    public static readonly Guid GatewayAccountId = Guid.Parse("10000000-0000-0000-0001-000000112300");
    public static readonly Guid ClientClearingAccountId = Guid.Parse("10000000-0000-0000-0002-000000210100");
    public static readonly Guid ClientPayableAccountId = Guid.Parse("10000000-0000-0000-0002-000000210200");
    public static readonly Guid RevenueAccountId = Guid.Parse("10000000-0000-0000-0004-000000410100");
    public static readonly Guid ExpenseAccountId = Guid.Parse("10000000-0000-0000-0006-000000610100");
}

public sealed class FinanceLegalEntityConfiguration : IEntityTypeConfiguration<FinanceLegalEntity>
{
    public void Configure(EntityTypeBuilder<FinanceLegalEntity> b)
    {
        b.ToTable("legal_entities", "finance"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(30).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.NameArabic).HasMaxLength(200).IsRequired(); b.Property(x => x.NameEnglish).HasMaxLength(200).IsRequired(); b.Property(x => x.BaseCurrencyCode).HasMaxLength(3).IsRequired();
        b.HasData(new FinanceLegalEntity(FinanceSeed.LegalEntityId, "MIS-EG", "شركة إم آي إس للتحصيل", "MIS Collection Firm", "EGP", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }
}

public sealed class FinanceCurrencyConfiguration : IEntityTypeConfiguration<FinanceCurrency>
{
    public void Configure(EntityTypeBuilder<FinanceCurrency> b)
    {
        b.ToTable("currencies", "finance"); b.HasKey(x => x.Code); b.Property(x => x.Code).HasMaxLength(3); b.Property(x => x.NameArabic).HasMaxLength(100); b.Property(x => x.NameEnglish).HasMaxLength(100);
        b.HasData(new FinanceCurrency("EGP", "الجنيه المصري", "Egyptian Pound", 2), new FinanceCurrency("USD", "الدولار الأمريكي", "US Dollar", 2), new FinanceCurrency("EUR", "اليورو", "Euro", 2));
    }
}

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> b)
    {
        b.ToTable("accounting_periods", "finance"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(80).IsRequired(); b.Property(x => x.Status).HasMaxLength(20).IsRequired(); b.Property(x => x.CloseReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.LegalEntityId, x.Year, x.PeriodNumber }).IsUnique(); b.HasIndex(x => new { x.LegalEntityId, x.StartDate, x.EndDate }); b.HasOne(x => x.LegalEntity).WithMany().HasForeignKey(x => x.LegalEntityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinanceAccountConfiguration : IEntityTypeConfiguration<FinanceAccount>
{
    public void Configure(EntityTypeBuilder<FinanceAccount> b)
    {
        b.ToTable("accounts", "finance"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(12).IsRequired(); b.Property(x => x.NameArabic).HasMaxLength(200).IsRequired(); b.Property(x => x.NameEnglish).HasMaxLength(200).IsRequired(); b.Property(x => x.AccountType).HasMaxLength(20); b.Property(x => x.NormalBalance).HasMaxLength(10); b.Property(x => x.ControlAccountType).HasMaxLength(40);
        b.HasIndex(x => new { x.LegalEntityId, x.Code }).IsUnique(); b.HasOne(x => x.LegalEntity).WithMany().HasForeignKey(x => x.LegalEntityId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        var accounts = new[]
        {
            Account(FinanceSeed.CashboxAccountId,"110100","النقدية والخزائن","Cashboxes","ASSET","DEBIT"),
            Account(FinanceSeed.BankAccountId,"110200","الحسابات البنكية","Bank Accounts","ASSET","DEBIT","TREASURY", client: true),
            Account(FinanceSeed.CustodyAccountId,"111100","عهدة نقدية لدى المحصلين","Collector Cash Custody","ASSET","DEBIT","COLLECTOR_CUSTODY", client: true, collector: true),
            Account(FinanceSeed.BankClearingAccountId,"112100","تحويلات بنكية تحت التسوية","Bank Clearing","ASSET","DEBIT","TREASURY", client: true),
            Account(FinanceSeed.ChequesAccountId,"112200","شيكات تحت التحصيل","Cheques Under Collection","ASSET","DEBIT","CHEQUES", client: true),
            Account(FinanceSeed.GatewayAccountId,"112300","مستحقات بوابات الدفع","Gateway Receivable","ASSET","DEBIT","GATEWAY", client: true),
            Account(FinanceSeed.ClientClearingAccountId,"210100","أموال عملاء تحت التسوية","Client Funds Clearing","LIABILITY","CREDIT","CLIENT_FUNDS", client: true),
            Account(FinanceSeed.ClientPayableAccountId,"210200","أموال عملاء مستحقة","Client Funds Payable","LIABILITY","CREDIT","CLIENT_FUNDS", client: true),
            Account(FinanceSeed.RevenueAccountId,"410100","إيراد عمولات التحصيل","Collection Commission Revenue","REVENUE","CREDIT"),
            Account(FinanceSeed.ExpenseAccountId,"610100","مصروفات تشغيلية عامة","General Operating Expenses","EXPENSE","DEBIT")
        };
        b.HasData(accounts);
    }
    private static FinanceAccount Account(Guid id, string code, string ar, string en, string type, string normal, string? control = null, bool client = false, bool collector = false)
    { var value = new FinanceAccount(id, FinanceSeed.LegalEntityId, code, ar, en, type, normal, true, control); value.SetDimensionRules(client, collector, false); return value; }
}

public sealed class AccountingEventConfiguration : IEntityTypeConfiguration<AccountingEvent>
{
    public void Configure(EntityTypeBuilder<AccountingEvent> b)
    {
        b.ToTable("accounting_events", "finance"); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(80); b.Property(x => x.SourceType).HasMaxLength(100); b.Property(x => x.IdempotencyKey).HasMaxLength(200); b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.PayloadSnapshot).HasColumnType("jsonb"); b.Property(x => x.Error).HasMaxLength(4000);
        b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.HasIndex(x => new { x.EventType, x.SourceType, x.SourceId, x.SourceVersion }).IsUnique(); b.HasIndex(x => new { x.Status, x.OccurredAt });
    }
}

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("journal_entries", "finance"); b.HasKey(x => x.Id); b.Property(x => x.JournalNumber).HasMaxLength(40); b.Property(x => x.EntryType).HasMaxLength(40); b.Property(x => x.CurrencyCode).HasMaxLength(3); b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.TotalDebit).HasPrecision(20,2); b.Property(x => x.TotalCredit).HasPrecision(20,2);
        b.HasIndex(x => new { x.LegalEntityId, x.JournalNumber }).IsUnique().HasFilter("\"JournalNumber\" IS NOT NULL"); b.HasIndex(x => new { x.Status, x.PostingDate }); b.HasIndex(x => x.AccountingEventId).IsUnique().HasFilter("\"AccountingEventId\" IS NOT NULL");
        b.HasOne(x => x.LegalEntity).WithMany().HasForeignKey(x => x.LegalEntityId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.AccountingEvent).WithOne().HasForeignKey<JournalEntry>(x => x.AccountingEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.PostedById).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ReversalOfJournal).WithMany().HasForeignKey(x => x.ReversalOfJournalId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.JournalEntry).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict); b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> b)
    {
        b.ToTable("journal_entry_lines", "finance", t => { t.HasCheckConstraint("CK_FinanceJournalLine_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)"); t.HasCheckConstraint("CK_FinanceJournalLine_BaseDebitCredit", "(\"BaseDebit\" > 0 AND \"BaseCredit\" = 0) OR (\"BaseCredit\" > 0 AND \"BaseDebit\" = 0)"); });
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.JournalEntryId, x.LineNumber }).IsUnique(); b.Property(x => x.Debit).HasPrecision(20,4); b.Property(x => x.Credit).HasPrecision(20,4); b.Property(x => x.BaseDebit).HasPrecision(20,2); b.Property(x => x.BaseCredit).HasPrecision(20,2); b.Property(x => x.ExchangeRate).HasPrecision(20,10); b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectorId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClientLedgerEntryConfiguration : IEntityTypeConfiguration<ClientLedgerEntry>
{
    public void Configure(EntityTypeBuilder<ClientLedgerEntry> b)
    {
        b.ToTable("client_ledger_entries", "finance", t => t.HasCheckConstraint("CK_ClientLedger_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)")); b.HasKey(x => x.Id); b.Property(x => x.EntryType).HasMaxLength(50); b.Property(x => x.Debit).HasPrecision(20,4); b.Property(x => x.Credit).HasPrecision(20,4); b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.HasIndex(x => x.JournalEntryLineId).IsUnique(); b.HasIndex(x => new { x.ClientId, x.TransactionDate }); b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.JournalEntryLine).WithOne().HasForeignKey<ClientLedgerEntry>(x => x.JournalEntryLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinancialAuditLogConfiguration : IEntityTypeConfiguration<FinancialAuditLog>
{
    public void Configure(EntityTypeBuilder<FinancialAuditLog> b)
    {
        b.ToTable("financial_audit_logs", "finance"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(120); b.Property(x => x.EntityType).HasMaxLength(120); b.Property(x => x.BeforeJson).HasColumnType("jsonb"); b.Property(x => x.AfterJson).HasColumnType("jsonb"); b.Property(x => x.Reason).HasMaxLength(1000); b.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt }); b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionPaymentFinanceConfiguration : IEntityTypeConfiguration<CollectionPayment>
{
    public void Configure(EntityTypeBuilder<CollectionPayment> b)
    {
        b.Property(x => x.CurrencyCode).HasMaxLength(3).HasDefaultValue("EGP").IsRequired();
        b.HasIndex(x => x.FinancialJournalEntryId).IsUnique().HasFilter("\"FinancialJournalEntryId\" IS NOT NULL");
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.FinancialJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.FinancialReversalJournalEntryId).IsUnique().HasFilter("\"FinancialReversalJournalEntryId\" IS NOT NULL");
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.FinancialReversalJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionFinancialReceiptConfiguration : IEntityTypeConfiguration<CollectionFinancialReceipt>
{
    public void Configure(EntityTypeBuilder<CollectionFinancialReceipt> b)
    {
        b.ToTable("collection_receipts", "finance", t =>
        {
            t.HasCheckConstraint("CK_CollectionReceipt_Amounts", "\"GrossAmount\" > 0 AND \"BaseAmount\" > 0 AND \"ExchangeRate\" > 0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.GrossAmount).HasPrecision(20,4); b.Property(x => x.BaseAmount).HasPrecision(20,2);
        b.Property(x => x.ExchangeRate).HasPrecision(20,10); b.Property(x => x.CurrencyCode).HasMaxLength(3); b.Property(x => x.Channel).HasMaxLength(40);
        b.Property(x => x.DestinationType).HasMaxLength(40); b.Property(x => x.DestinationReference).HasMaxLength(200); b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => x.CollectionPaymentId).IsUnique(); b.HasIndex(x => x.JournalEntryId).IsUnique();
        b.HasIndex(x => x.ClearingJournalEntryId).IsUnique().HasFilter("\"ClearingJournalEntryId\" IS NOT NULL");
        b.HasIndex(x => x.ReversalJournalEntryId).IsUnique().HasFilter("\"ReversalJournalEntryId\" IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.PostedAt }); b.HasIndex(x => new { x.ClientId, x.Status });
        b.HasOne(x => x.CollectionPayment).WithOne().HasForeignKey<CollectionFinancialReceipt>(x => x.CollectionPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.JournalEntry).WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ClearingJournalEntry).WithMany().HasForeignKey(x => x.ClearingJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReversalJournalEntry).WithMany().HasForeignKey(x => x.ReversalJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Allocations).WithOne(x => x.Receipt).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CollectionPaymentAllocationConfiguration : IEntityTypeConfiguration<CollectionPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<CollectionPaymentAllocation> b)
    {
        b.ToTable("collection_payment_allocations", "finance", t => t.HasCheckConstraint("CK_CollectionAllocation_Amount", "\"Amount\" > 0"));
        b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(20,4); b.Property(x => x.OutstandingBefore).HasPrecision(20,4); b.Property(x => x.OverdueBefore).HasPrecision(20,4);
        b.HasIndex(x => new { x.ReceiptId, x.LineNumber }).IsUnique(); b.HasIndex(x => new { x.ReceiptId, x.CaseId }).IsUnique(); b.HasIndex(x => x.CaseId);
        b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectorCustodyAccountConfiguration : IEntityTypeConfiguration<CollectorCustodyAccount>
{
    public void Configure(EntityTypeBuilder<CollectorCustodyAccount> b)
    {
        b.ToTable("collector_custody_accounts", "finance", t => t.HasCheckConstraint("CK_CustodyAccount_Limits", "\"SoftLimit\" >= 0 AND \"HardLimit\" > 0 AND \"HardLimit\" >= \"SoftLimit\""));
        b.HasKey(x => x.Id); b.Property(x => x.CurrencyCode).HasMaxLength(3); b.Property(x => x.SoftLimit).HasPrecision(20,4); b.Property(x => x.HardLimit).HasPrecision(20,4); b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => new { x.CollectorId, x.CurrencyCode }).IsUnique(); b.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectorCustodyTransactionConfiguration : IEntityTypeConfiguration<CollectorCustodyTransaction>
{
    public void Configure(EntityTypeBuilder<CollectorCustodyTransaction> b)
    {
        b.ToTable("collector_custody_transactions", "finance", t => t.HasCheckConstraint("CK_CustodyTransaction_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)"));
        b.HasKey(x => x.Id); b.Property(x => x.TransactionType).HasMaxLength(40); b.Property(x => x.Debit).HasPrecision(20,4); b.Property(x => x.Credit).HasPrecision(20,4);
        b.HasIndex(x => x.JournalEntryLineId).IsUnique(); b.HasIndex(x => new { x.CustodyAccountId, x.TransactionDate }); b.HasIndex(x => x.ReceiptId);
        b.HasOne(x => x.CustodyAccount).WithMany().HasForeignKey(x => x.CustodyAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.JournalEntryLine).WithOne().HasForeignKey<CollectorCustodyTransaction>(x => x.JournalEntryLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionClearingEventConfiguration : IEntityTypeConfiguration<CollectionClearingEvent>
{
    public void Configure(EntityTypeBuilder<CollectionClearingEvent> b)
    {
        b.ToTable("collection_clearing_events", "finance", t => t.HasCheckConstraint("CK_CollectionClearing_Amount", "\"Amount\" > 0"));
        b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(20,4); b.Property(x => x.Reference).HasMaxLength(200);
        b.HasIndex(x => x.ReceiptId).IsUnique(); b.HasIndex(x => new { x.ToAccountId, x.OccurredOn, x.Reference, x.Amount }).IsUnique();
        b.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FromAccount).WithMany().HasForeignKey(x => x.FromAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToAccount).WithMany().HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.JournalEntry).WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
