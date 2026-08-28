using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionFinanceCustodyAndClearing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinancialReversalJournalEntryId",
                table: "CollectionPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "collection_receipts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(20,10)", precision: 20, scale: 10, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DestinationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DestinationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CollectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClearingJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClearedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReversedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_receipts", x => x.Id);
                    table.CheckConstraint("CK_CollectionReceipt_Amounts", "\"GrossAmount\" > 0 AND \"BaseAmount\" > 0 AND \"ExchangeRate\" > 0");
                    table.ForeignKey(
                        name: "FK_collection_receipts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_receipts_CollectionClientOrganizations_ClientId",
                        column: x => x.ClientId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_receipts_CollectionPayments_CollectionPaymentId",
                        column: x => x.CollectionPaymentId,
                        principalTable: "CollectionPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_receipts_Users_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_receipts_journal_entries_ClearingJournalEntryId",
                        column: x => x.ClearingJournalEntryId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_receipts_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_receipts_journal_entries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collector_custody_accounts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SoftLimit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    HardLimit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collector_custody_accounts", x => x.Id);
                    table.CheckConstraint("CK_CustodyAccount_Limits", "\"SoftLimit\" >= 0 AND \"HardLimit\" > 0 AND \"HardLimit\" >= \"SoftLimit\"");
                    table.ForeignKey(
                        name: "FK_collector_custody_accounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collector_custody_accounts_Users_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection_clearing_events",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_clearing_events", x => x.Id);
                    table.CheckConstraint("CK_CollectionClearing_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_collection_clearing_events_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_clearing_events_accounts_FromAccountId",
                        column: x => x.FromAccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_clearing_events_accounts_ToAccountId",
                        column: x => x.ToAccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_clearing_events_collection_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "finance",
                        principalTable: "collection_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_clearing_events_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection_payment_allocations",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    OutstandingBefore = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    OverdueBefore = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_payment_allocations", x => x.Id);
                    table.CheckConstraint("CK_CollectionAllocation_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_collection_payment_allocations_CollectionCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CollectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_payment_allocations_collection_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "finance",
                        principalTable: "collection_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collector_custody_transactions",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustodyAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collector_custody_transactions", x => x.Id);
                    table.CheckConstraint("CK_CustodyTransaction_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_collector_custody_transactions_collection_receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "finance",
                        principalTable: "collection_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collector_custody_transactions_collector_custody_accounts_C~",
                        column: x => x.CustodyAccountId,
                        principalSchema: "finance",
                        principalTable: "collector_custody_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collector_custody_transactions_journal_entry_lines_JournalE~",
                        column: x => x.JournalEntryLineId,
                        principalSchema: "finance",
                        principalTable: "journal_entry_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "finance",
                table: "accounts",
                columns: new[] { "Id", "AccountType", "Code", "ControlAccountType", "IsActive", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector" },
                values: new object[] { new Guid("10000000-0000-0000-0001-000000110200"), "ASSET", "110200", "TREASURY", true, new Guid("10000000-0000-0000-0000-000000000001"), "الحسابات البنكية", "Bank Accounts", "DEBIT", null, true, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPayments_FinancialReversalJournalEntryId",
                table: "CollectionPayments",
                column: "FinancialReversalJournalEntryId",
                unique: true,
                filter: "\"FinancialReversalJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_CreatedById",
                schema: "finance",
                table: "collection_clearing_events",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_FromAccountId",
                schema: "finance",
                table: "collection_clearing_events",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_JournalEntryId",
                schema: "finance",
                table: "collection_clearing_events",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_OccurredOn_Reference",
                schema: "finance",
                table: "collection_clearing_events",
                columns: new[] { "OccurredOn", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_ReceiptId",
                schema: "finance",
                table: "collection_clearing_events",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_ToAccountId",
                schema: "finance",
                table: "collection_clearing_events",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_payment_allocations_CaseId",
                schema: "finance",
                table: "collection_payment_allocations",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_payment_allocations_ReceiptId_CaseId",
                schema: "finance",
                table: "collection_payment_allocations",
                columns: new[] { "ReceiptId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_payment_allocations_ReceiptId_LineNumber",
                schema: "finance",
                table: "collection_payment_allocations",
                columns: new[] { "ReceiptId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_BranchId",
                schema: "finance",
                table: "collection_receipts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_ClearingJournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "ClearingJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_ClientId_Status",
                schema: "finance",
                table: "collection_receipts",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_CollectionPaymentId",
                schema: "finance",
                table: "collection_receipts",
                column: "CollectionPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_CollectorId",
                schema: "finance",
                table: "collection_receipts",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_JournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_ReversalJournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_Status_PostedAt",
                schema: "finance",
                table: "collection_receipts",
                columns: new[] { "Status", "PostedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_collector_custody_accounts_BranchId",
                schema: "finance",
                table: "collector_custody_accounts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_collector_custody_accounts_CollectorId_CurrencyCode",
                schema: "finance",
                table: "collector_custody_accounts",
                columns: new[] { "CollectorId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collector_custody_transactions_CustodyAccountId_Transaction~",
                schema: "finance",
                table: "collector_custody_transactions",
                columns: new[] { "CustodyAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_collector_custody_transactions_JournalEntryLineId",
                schema: "finance",
                table: "collector_custody_transactions",
                column: "JournalEntryLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collector_custody_transactions_ReceiptId",
                schema: "finance",
                table: "collector_custody_transactions",
                column: "ReceiptId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionPayments_journal_entries_FinancialReversalJournal~",
                table: "CollectionPayments",
                column: "FinancialReversalJournalEntryId",
                principalSchema: "finance",
                principalTable: "journal_entries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionPayments_journal_entries_FinancialReversalJournal~",
                table: "CollectionPayments");

            migrationBuilder.DropTable(
                name: "collection_clearing_events",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "collection_payment_allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "collector_custody_transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "collection_receipts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "collector_custody_accounts",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "IX_CollectionPayments_FinancialReversalJournalEntryId",
                table: "CollectionPayments");

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000110200"));

            migrationBuilder.DropColumn(
                name: "FinancialReversalJournalEntryId",
                table: "CollectionPayments");
        }
    }
}
