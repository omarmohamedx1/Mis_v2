using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingBookAndRecurring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCashAccount",
                schema: "finance",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountingRecurringExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingRecurringExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingRecurringExpenses_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingRecurringExpenses_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingRecurringExpenses_accounts_CashAccountId",
                        column: x => x.CashAccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingBookEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedAdvanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecurringExpenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingBookEntries", x => x.Id);
                    table.CheckConstraint("CK_AccountingBookEntries_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_AccountingBookEntries_RelatedAdvanceId",
                        column: x => x.RelatedAdvanceId,
                        principalTable: "AccountingBookEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_AccountingRecurringExpenses_Recurring~",
                        column: x => x.RecurringExpenseId,
                        principalTable: "AccountingRecurringExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_accounts_CounterAccountId",
                        column: x => x.CounterAccountId,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingBookEntries_journal_entries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000110100"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000110200"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000111100"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000112100"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000112200"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000112300"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0002-000000210100"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0002-000000210200"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0004-000000410100"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.UpdateData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610100"),
                column: "IsCashAccount",
                value: false);

            migrationBuilder.InsertData(
                schema: "finance",
                table: "accounts",
                columns: new[] { "Id", "AccountType", "Code", "ControlAccountType", "IsActive", "IsCashAccount", "LegalEntityId", "NameArabic", "NameEnglish", "NormalBalance", "ParentId", "PostingAllowed", "RequiresBranch", "RequiresClient", "RequiresCollector" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0001-000000110300"), "ASSET", "110300", null, true, true, new Guid("10000000-0000-0000-0000-000000000001"), "خزينة الشركة التشغيلية", "Company operating cash", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0001-000000110400"), "ASSET", "110400", null, true, true, new Guid("10000000-0000-0000-0000-000000000001"), "بنك الشركة التشغيلي", "Company operating bank", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0001-000000113100"), "ASSET", "113100", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "سلف الموظفين", "Employee advances", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0003-000000310100"), "EQUITY", "310100", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "أرصدة افتتاحية", "Opening balances", "CREDIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610110"), "EXPENSE", "610110", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "مصاريف نثرية", "Petty expenses", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610120"), "EXPENSE", "610120", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "مصاريف شهرية ثابتة", "Fixed monthly expenses", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610130"), "EXPENSE", "610130", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "انتقالات", "Transportation expenses", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610140"), "EXPENSE", "610140", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "مرتبات وأجور", "Salaries and wages", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610150"), "EXPENSE", "610150", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "مكافآت", "Bonuses and rewards", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610160"), "EXPENSE", "610160", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "مصاريف قضائية", "Judicial expenses", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610170"), "EXPENSE", "610170", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "عمولات المحصلين", "Collector commissions", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610180"), "EXPENSE", "610180", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "عمولات السوبر فايزر", "Supervisor commissions", "DEBIT", null, true, false, false, false },
                    { new Guid("10000000-0000-0000-0006-000000610190"), "EXPENSE", "610190", null, true, false, new Guid("10000000-0000-0000-0000-000000000001"), "عمولات ضمن المرتبات", "Commissions included in payroll", "DEBIT", null, true, false, false, false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_AccountId",
                table: "AccountingBookEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_Category_TransactionDate",
                table: "AccountingBookEntries",
                columns: new[] { "Category", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_CounterAccountId",
                table: "AccountingBookEntries",
                column: "CounterAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_CreatedById",
                table: "AccountingBookEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_DocumentNumber",
                table: "AccountingBookEntries",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_EmployeeId",
                table: "AccountingBookEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_JournalEntryId",
                table: "AccountingBookEntries",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_RecurringExpenseId",
                table: "AccountingBookEntries",
                column: "RecurringExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_RelatedAdvanceId",
                table: "AccountingBookEntries",
                column: "RelatedAdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_SourceKey",
                table: "AccountingBookEntries",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingRecurringExpenses_AccountId",
                table: "AccountingRecurringExpenses",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingRecurringExpenses_CashAccountId",
                table: "AccountingRecurringExpenses",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingRecurringExpenses_CreatedById",
                table: "AccountingRecurringExpenses",
                column: "CreatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingBookEntries");

            migrationBuilder.DropTable(
                name: "AccountingRecurringExpenses");

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000110300"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000110400"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0001-000000113100"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0003-000000310100"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610110"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610120"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610130"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610140"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610150"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610160"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610170"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610180"));

            migrationBuilder.DeleteData(
                schema: "finance",
                table: "accounts",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0006-000000610190"));

            migrationBuilder.DropColumn(
                name: "IsCashAccount",
                schema: "finance",
                table: "accounts");
        }
    }
}
