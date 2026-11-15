using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActScorePeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActScorePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RulesJson = table.Column<string>(type: "jsonb", nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreparedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActScorePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActScorePeriods_CollectionClientOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActScorePeriods_CollectionPortfolios_PortfolioId_Organizati~",
                        columns: x => new { x.PortfolioId, x.OrganizationId },
                        principalTable: "CollectionPortfolios",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActScorePeriods_Users_PreparedBy",
                        column: x => x.PreparedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActScorePeriods_OrganizationId_PortfolioId_ReportingPeriod",
                table: "ActScorePeriods",
                columns: new[] { "OrganizationId", "PortfolioId", "ReportingPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActScorePeriods_PortfolioId_OrganizationId",
                table: "ActScorePeriods",
                columns: new[] { "PortfolioId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActScorePeriods_PreparedBy",
                table: "ActScorePeriods",
                column: "PreparedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActScorePeriods");
        }
    }
}
