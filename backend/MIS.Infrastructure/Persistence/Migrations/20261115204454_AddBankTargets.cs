using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTargetConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    ValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CopiedFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTargetConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_BankTargetConfigurations_CopiedFro~",
                        column: x => x.CopiedFromId,
                        principalTable: "BankTargetConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_CollectionClientOrganizations_Orga~",
                        column: x => x.OrganizationId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_CollectionPortfolios_PortfolioId_O~",
                        columns: x => new { x.PortfolioId, x.OrganizationId },
                        principalTable: "CollectionPortfolios",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetConfigurations_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankTargetAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ValuesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTargetAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTargetAudits_BankTargetConfigurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "BankTargetConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTargetAudits_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetAudits_ActorId",
                table: "BankTargetAudits",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetAudits_ConfigurationId_OccurredAt",
                table: "BankTargetAudits",
                columns: new[] { "ConfigurationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_ApprovedBy",
                table: "BankTargetConfigurations",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_CopiedFromId",
                table: "BankTargetConfigurations",
                column: "CopiedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_CreatedBy",
                table: "BankTargetConfigurations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_OrganizationId_PortfolioId_Portfol~",
                table: "BankTargetConfigurations",
                columns: new[] { "OrganizationId", "PortfolioId", "PortfolioType", "ReportingPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_PortfolioId_OrganizationId",
                table: "BankTargetConfigurations",
                columns: new[] { "PortfolioId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_ReviewedBy",
                table: "BankTargetConfigurations",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BankTargetConfigurations_UpdatedBy",
                table: "BankTargetConfigurations",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTargetAudits");

            migrationBuilder.DropTable(
                name: "BankTargetConfigurations");
        }
    }
}
