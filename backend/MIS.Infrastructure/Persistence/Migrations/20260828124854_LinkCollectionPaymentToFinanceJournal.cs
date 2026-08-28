using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkCollectionPaymentToFinanceJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CollectionPayments_FinancialJournalEntryId",
                table: "CollectionPayments",
                column: "FinancialJournalEntryId",
                unique: true,
                filter: "\"FinancialJournalEntryId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionPayments_journal_entries_FinancialJournalEntryId",
                table: "CollectionPayments",
                column: "FinancialJournalEntryId",
                principalSchema: "finance",
                principalTable: "journal_entries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionPayments_journal_entries_FinancialJournalEntryId",
                table: "CollectionPayments");

            migrationBuilder.DropIndex(
                name: "IX_CollectionPayments_FinancialJournalEntryId",
                table: "CollectionPayments");
        }
    }
}
