using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimaryClassification",
                table: "CollectionPortfolios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubClassification",
                table: "CollectionPortfolios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPortfolios_OrganizationId_PrimaryClassification_S~",
                table: "CollectionPortfolios",
                columns: new[] { "OrganizationId", "PrimaryClassification", "SubClassification" },
                unique: true,
                filter: "\"PrimaryClassification\" IS NOT NULL AND \"SubClassification\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionPortfolios_OrganizationId_PrimaryClassification_S~",
                table: "CollectionPortfolios");

            migrationBuilder.DropColumn(
                name: "PrimaryClassification",
                table: "CollectionPortfolios");

            migrationBuilder.DropColumn(
                name: "SubClassification",
                table: "CollectionPortfolios");
        }
    }
}
