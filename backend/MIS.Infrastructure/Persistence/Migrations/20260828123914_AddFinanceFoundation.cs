using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialJournalEntryId",
                table: "CollectionPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounting_events",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                schema: "finance",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinorUnits = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "financial_audit_logs",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_audit_logs_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_entities",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "accounting_periods",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_periods_legal_entities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalSchema: "finance",
                        principalTable: "legal_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalBalance = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    ControlAccountType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RequiresClient = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresCollector = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresBranch = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_accounts_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_legal_entities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalSchema: "finance",
                        principalTable: "legal_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EntryType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TotalDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReversalOfJournalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_entries_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_Users_PostedById",
                        column: x => x.PostedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_accounting_events_AccountingEventId",
                        column: x => x.AccountingEventId,
                        principalSchema: "finance",
                        principalTable: "accounting_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_accounting_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "finance",
                        principalTable: "accounting_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_journal_entries_ReversalOfJournalId",
                        column: x => x.ReversalOfJournalId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_legal_entities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalSchema: "finance",
                        principalTable: "legal_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    BaseDebit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    BaseCredit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(20,10)", precision: 20, scale: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.Id);
                    table.CheckConstraint("CK_FinanceJournalLine_BaseDebitCredit", "(\"BaseDebit\" > 0 AND \"BaseCredit\" = 0) OR (\"BaseCredit\" > 0 AND \"BaseDebit\" = 0)");
                    table.CheckConstraint("CK_FinanceJournalLine_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_CollectionClientOrganizations_ClientId",
                        column: x => x.ClientId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_Users_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_ledger_entries",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_ledger_entries", x => x.Id);
                    table.CheckConstraint("CK_ClientLedger_DebitCredit", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_client_ledger_entries_CollectionClientOrganizations_ClientId",
                        column: x => x.ClientId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_ledger_entries_journal_entry_lines_JournalEntryLineId",
                        column: x => x.JournalEntryLineId,
                        principalSchema: "finance",
                        principalTable: "journal_entry_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "finance",
                table: "currencies",
                columns: new[] { "Code", "IsActive", "MinorUnits", "NameArabic", "NameEnglish" },
                values: new object[,]
                {
                    { "EGP", true, 2, "الجنيه المصري", "Egyptian Pound" },
                    { "EUR", true, 2, "اليورو", "Euro" },
                    { "USD", true, 2, "الدولار الأمريكي", "US Dollar" }
                });

            migrationBuilder.InsertData(
                schema: "finance",
                table: "legal_entities",
                columns: new[] { "Id", "BaseCurrencyCode", "Code", "CreatedAt", "IsActive", "NameArabic", "NameEnglish" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), "EGP", "MIS-EG", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "شركة إم آي إس للتحصيل", "MIS Collection Firm" });

            migrationBuilder.InsertData(
                schema: "finance",
                table: "accounts",
                columns: new[] { "Id", "AccountType", "Code", "ControlAccountType", "IsActive", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0001-000000110100"), "ASSET", "110100", null, true, new Guid("10000000-0000-0000-0000-000000000001"), "النقدية والخزائن", "Cashboxes", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0001-000000111100"), "ASSET", "111100", "COLLECTOR_CUSTODY", true, new Guid("10000000-0000-0000-0000-000000000001"), "عهدة نقدية لدى المحصلين", "Collector Cash Custody", "DEBIT", null, true, false, true, true },
                    { new Guid("10000000-0000-0000-0001-000000112100"), "ASSET", "112100", "TREASURY", true, new Guid("10000000-0000-0000-0000-000000000001"), "تحويلات بنكية تحت التسوية", "Bank Clearing", "DEBIT", null, true, false, true, false },
                    { new Guid("10000000-0000-0000-0001-000000112200"), "ASSET", "112200", "CHEQUES", true, new Guid("10000000-0000-0000-0000-000000000001"), "شيكات تحت التحصيل", "Cheques Under Collection", "DEBIT", null, true, false, true, false },
                    { new Guid("10000000-0000-0000-0001-000000112300"), "ASSET", "112300", "GATEWAY", true, new Guid("10000000-0000-0000-0000-000000000001"), "مستحقات بوابات الدفع", "Gateway Receivable", "DEBIT", null, true, false, true, false },
                    { new Guid("10000000-0000-0000-0002-000000210100"), "LIABILITY", "210100", "CLIENT_FUNDS", true, new Guid("10000000-0000-0000-0000-000000000001"), "أموال عملاء تحت التسوية", "Client Funds Clearing", "CREDIT", null, true, false, true, false },
                    { new Guid("10000000-0000-0000-0002-000000210200"), "LIABILITY", "210200", "CLIENT_FUNDS", true, new Guid("10000000-0000-0000-0000-000000000001"), "أموال عملاء مستحقة", "Client Funds Payable", "CREDIT", null, true, false, true, false },
                    { new Guid("10000000-0000-0000-0004-000000410100"), "REVENUE", "410100", null, true, new Guid("10000000-0000-0000-0000-000000000001"), "إيراد عمولات التحصيل", "Collection Commission Revenue", "CREDIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610100"), "EXPENSE", "610100", null, true, new Guid("10000000-0000-0000-0000-000000000001"), "مصروفات تشغيلية عامة", "General Operating Expenses", "DEBIT", null, true, false, false, false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_events_EventType_SourceType_SourceId_SourceVersi~",
                schema: "finance",
                table: "accounting_events",
                columns: new[] { "EventType", "SourceType", "SourceId", "SourceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_events_IdempotencyKey",
                schema: "finance",
                table: "accounting_events",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_events_Status_OccurredAt",
                schema: "finance",
                table: "accounting_events",
                columns: new[] { "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_LegalEntityId_StartDate_EndDate",
                schema: "finance",
                table: "accounting_periods",
                columns: new[] { "LegalEntityId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_LegalEntityId_Year_PeriodNumber",
                schema: "finance",
                table: "accounting_periods",
                columns: new[] { "LegalEntityId", "Year", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_LegalEntityId_Code",
                schema: "finance",
                table: "accounts",
                columns: new[] { "LegalEntityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ParentId",
                schema: "finance",
                table: "accounts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_client_ledger_entries_ClientId_TransactionDate",
                schema: "finance",
                table: "client_ledger_entries",
                columns: new[] { "ClientId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_client_ledger_entries_JournalEntryLineId",
                schema: "finance",
                table: "client_ledger_entries",
                column: "JournalEntryLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_logs_ActorId",
                schema: "finance",
                table: "financial_audit_logs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_logs_EntityType_EntityId_CreatedAt",
                schema: "finance",
                table: "financial_audit_logs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_AccountingEventId",
                schema: "finance",
                table: "journal_entries",
                column: "AccountingEventId",
                unique: true,
                filter: "\"AccountingEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_ApprovedById",
                schema: "finance",
                table: "journal_entries",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CreatedById",
                schema: "finance",
                table: "journal_entries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_LegalEntityId_JournalNumber",
                schema: "finance",
                table: "journal_entries",
                columns: new[] { "LegalEntityId", "JournalNumber" },
                unique: true,
                filter: "\"JournalNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_PeriodId",
                schema: "finance",
                table: "journal_entries",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_PostedById",
                schema: "finance",
                table: "journal_entries",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_ReversalOfJournalId",
                schema: "finance",
                table: "journal_entries",
                column: "ReversalOfJournalId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_Status_PostingDate",
                schema: "finance",
                table: "journal_entries",
                columns: new[] { "Status", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_AccountId",
                schema: "finance",
                table: "journal_entry_lines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_BranchId",
                schema: "finance",
                table: "journal_entry_lines",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_ClientId",
                schema: "finance",
                table: "journal_entry_lines",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_CollectorId",
                schema: "finance",
                table: "journal_entry_lines",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_JournalEntryId_LineNumber",
                schema: "finance",
                table: "journal_entry_lines",
                columns: new[] { "JournalEntryId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_entities_Code",
                schema: "finance",
                table: "legal_entities",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_ledger_entries",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "currencies",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_audit_logs",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "journal_entry_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "accounting_events",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "accounting_periods",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "legal_entities",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "FinancialJournalEntryId",
                table: "CollectionPayments");
        }
    }
}
