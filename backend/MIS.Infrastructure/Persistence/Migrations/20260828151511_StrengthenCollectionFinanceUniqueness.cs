using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StrengthenCollectionFinanceUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collection_receipts_ClearingJournalEntryId",
                schema: "finance",
                table: "collection_receipts");

            migrationBuilder.DropIndex(
                name: "IX_collection_receipts_JournalEntryId",
                schema: "finance",
                table: "collection_receipts");

            migrationBuilder.DropIndex(
                name: "IX_collection_receipts_ReversalJournalEntryId",
                schema: "finance",
                table: "collection_receipts");

            migrationBuilder.DropIndex(
                name: "IX_collection_clearing_events_OccurredOn_Reference",
                schema: "finance",
                table: "collection_clearing_events");

            migrationBuilder.DropIndex(
                name: "IX_collection_clearing_events_ToAccountId",
                schema: "finance",
                table: "collection_clearing_events");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_ClearingJournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "ClearingJournalEntryId",
                unique: true,
                filter: "\"ClearingJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_JournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_ReversalJournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "ReversalJournalEntryId",
                unique: true,
                filter: "\"ReversalJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_ToAccountId_OccurredOn_Reference~",
                schema: "finance",
                table: "collection_clearing_events",
                columns: new[] { "ToAccountId", "OccurredOn", "Reference", "Amount" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collection_receipts_ClearingJournalEntryId",
                schema: "finance",
                table: "collection_receipts");

            migrationBuilder.DropIndex(
                name: "IX_collection_receipts_JournalEntryId",
                schema: "finance",
                table: "collection_receipts");

            migrationBuilder.DropIndex(
                name: "IX_collection_receipts_ReversalJournalEntryId",
                schema: "finance",
                table: "collection_receipts");

            migrationBuilder.DropIndex(
                name: "IX_collection_clearing_events_ToAccountId_OccurredOn_Reference~",
                schema: "finance",
                table: "collection_clearing_events");

            migrationBuilder.CreateIndex(
                name: "IX_collection_receipts_ClearingJournalEntryId",
                schema: "finance",
                table: "collection_receipts",
                column: "ClearingJournalEntryId");

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
                name: "IX_collection_clearing_events_OccurredOn_Reference",
                schema: "finance",
                table: "collection_clearing_events",
                columns: new[] { "OccurredOn", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_clearing_events_ToAccountId",
                schema: "finance",
                table: "collection_clearing_events",
                column: "ToAccountId");
        }
    }
}
