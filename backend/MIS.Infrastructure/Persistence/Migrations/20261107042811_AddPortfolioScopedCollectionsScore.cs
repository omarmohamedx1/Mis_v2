using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioScopedCollectionsScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionScoreValues_OrganizationId_OrganizationType_Portf~",
                table: "CollectionScoreValues");

            migrationBuilder.DropIndex(
                name: "IX_CollectionScoreValues_OrganizationId_PortfolioType_Reportin~",
                table: "CollectionScoreValues");

            migrationBuilder.DropIndex(
                name: "IX_CollectionScoreAudits_OrganizationId_PortfolioType_Reportin~",
                table: "CollectionScoreAudits");

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioId",
                table: "CollectionScoreValues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioId",
                table: "CollectionScoreAudits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CollectionPortfolios_Id_OrganizationId",
                table: "CollectionPortfolios",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreValues_OrganizationId_OrganizationType_Portf~",
                table: "CollectionScoreValues",
                columns: new[] { "OrganizationId", "OrganizationType", "PortfolioType", "PortfolioId", "ReportingPeriod", "Bucket", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreValues_OrganizationId_PortfolioId_PortfolioT~",
                table: "CollectionScoreValues",
                columns: new[] { "OrganizationId", "PortfolioId", "PortfolioType", "ReportingPeriod", "Bucket" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreValues_PortfolioId_OrganizationId",
                table: "CollectionScoreValues",
                columns: new[] { "PortfolioId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreAudits_OrganizationId_PortfolioId_PortfolioT~",
                table: "CollectionScoreAudits",
                columns: new[] { "OrganizationId", "PortfolioId", "PortfolioType", "ReportingPeriod", "Bucket", "Category", "ModifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreAudits_PortfolioId_OrganizationId",
                table: "CollectionScoreAudits",
                columns: new[] { "PortfolioId", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionScoreAudits_CollectionPortfolios_PortfolioId_Orga~",
                table: "CollectionScoreAudits",
                columns: new[] { "PortfolioId", "OrganizationId" },
                principalTable: "CollectionPortfolios",
                principalColumns: new[] { "Id", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionScoreValues_CollectionPortfolios_PortfolioId_Orga~",
                table: "CollectionScoreValues",
                columns: new[] { "PortfolioId", "OrganizationId" },
                principalTable: "CollectionPortfolios",
                principalColumns: new[] { "Id", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Portfolio-scoped Score values cannot be safely downgraded without an explicit data reconciliation.");
        }
    }
}
