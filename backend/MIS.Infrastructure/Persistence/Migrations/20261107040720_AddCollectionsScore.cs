using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionsScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionScoreAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PortfolioType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    Bucket = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionScoreAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionScoreAudits_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionScoreValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PortfolioType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    Bucket = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CaseCount = table.Column<int>(type: "integer", nullable: true),
                    ActualBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TargetPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    TargetBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Missing = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ActualVsTarget = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionScoreValues", x => x.Id);
                    table.CheckConstraint("CK_CollectionScoreValues_CaseCount", "\"CaseCount\" IS NULL OR \"CaseCount\" >= 0");
                    table.CheckConstraint("CK_CollectionScoreValues_NonNegativeAmounts", "(\"ActualBalance\" IS NULL OR \"ActualBalance\" >= 0) AND (\"TargetBalance\" IS NULL OR \"TargetBalance\" >= 0) AND (\"Missing\" IS NULL OR \"Missing\" >= 0)");
                    table.CheckConstraint("CK_CollectionScoreValues_Percentages", "(\"TargetPercent\" IS NULL OR (\"TargetPercent\" >= 0 AND \"TargetPercent\" <= 100)) AND (\"Percent\" IS NULL OR (\"Percent\" >= 0 AND \"Percent\" <= 100))");
                    table.CheckConstraint("CK_CollectionScoreValues_WeightScore", "(\"Weight\" IS NULL OR \"Weight\" >= 0) AND (\"Score\" IS NULL OR \"Score\" >= 0)");
                    table.ForeignKey(
                        name: "FK_CollectionScoreValues_CollectionClientOrganizations_Organiz~",
                        column: x => x.OrganizationId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionScoreValues_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreAudits_ModifiedByUserId",
                table: "CollectionScoreAudits",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreAudits_OrganizationId_PortfolioType_Reportin~",
                table: "CollectionScoreAudits",
                columns: new[] { "OrganizationId", "PortfolioType", "ReportingPeriod", "Bucket", "Category", "ModifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreValues_ModifiedByUserId",
                table: "CollectionScoreValues",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreValues_OrganizationId_OrganizationType_Portf~",
                table: "CollectionScoreValues",
                columns: new[] { "OrganizationId", "OrganizationType", "PortfolioType", "ReportingPeriod", "Bucket", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionScoreValues_OrganizationId_PortfolioType_Reportin~",
                table: "CollectionScoreValues",
                columns: new[] { "OrganizationId", "PortfolioType", "ReportingPeriod", "Bucket" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionScoreAudits");

            migrationBuilder.DropTable(
                name: "CollectionScoreValues");
        }
    }
}
