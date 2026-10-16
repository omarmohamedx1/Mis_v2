using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAccountingRecordingAndDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "AccountingBookEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "AccountingBookEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBookEntries_DeletedById",
                table: "AccountingBookEntries",
                column: "DeletedById");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingBookEntries_Users_DeletedById",
                table: "AccountingBookEntries",
                column: "DeletedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingBookEntries_Users_DeletedById",
                table: "AccountingBookEntries");

            migrationBuilder.DropIndex(
                name: "IX_AccountingBookEntries_DeletedById",
                table: "AccountingBookEntries");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AccountingBookEntries");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "AccountingBookEntries");
        }
    }
}
