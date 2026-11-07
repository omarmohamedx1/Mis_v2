using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioScopedBankPortfolioImportResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankPortfolioImports_BankId_FileHash",
                table: "BankPortfolioImports");

            migrationBuilder.AddColumn<int>(
                name: "DuplicateRows",
                table: "BankPortfolioImports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ImportedRows",
                table: "BankPortfolioImports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InvalidRows",
                table: "BankPortfolioImports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioId",
                table: "BankPortfolioImports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedRows",
                table: "BankPortfolioImports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BankPortfolioImports_BankId_PortfolioId_FileHash",
                table: "BankPortfolioImports",
                columns: new[] { "BankId", "PortfolioId", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankPortfolioImports_BankId_PortfolioId_UploadedAt",
                table: "BankPortfolioImports",
                columns: new[] { "BankId", "PortfolioId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BankPortfolioImports_PortfolioId_BankId",
                table: "BankPortfolioImports",
                columns: new[] { "PortfolioId", "BankId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BankPortfolioImports_CollectionPortfolios_PortfolioId_BankId",
                table: "BankPortfolioImports",
                columns: new[] { "PortfolioId", "BankId" },
                principalTable: "CollectionPortfolios",
                principalColumns: new[] { "Id", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Portfolio bindings and import results cannot be safely downgraded after they are populated.");
        }
    }
}
