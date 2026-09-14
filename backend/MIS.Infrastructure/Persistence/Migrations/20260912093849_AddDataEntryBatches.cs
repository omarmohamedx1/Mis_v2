using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataEntryBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "CollectionCustomers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataEntrySource",
                table: "CollectionCustomers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "CollectionCustomers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CollectionCustomers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataEntryBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryClassification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SubClassification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    InvalidRows = table.Column<int>(type: "integer", nullable: false),
                    CreatedCustomerCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedCaseCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DistributedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataEntryBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataEntryBatches_CollectionClientOrganizations_Organization~",
                        column: x => x.OrganizationId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataEntryBatches_CollectionPortfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "CollectionPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataEntryBatches_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataEntryBatches_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataEntryNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MessageArabic = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MessageEnglish = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataEntryNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataEntryNotifications_DataEntryBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "DataEntryBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataEntryNotifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataEntryRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NationalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OutstandingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OverdueBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DaysPastDue = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    CollectionCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataEntryRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataEntryRows_CollectionCases_CollectionCaseId",
                        column: x => x.CollectionCaseId,
                        principalTable: "CollectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataEntryRows_CollectionCustomers_CollectionCustomerId",
                        column: x => x.CollectionCustomerId,
                        principalTable: "CollectionCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DataEntryRows_DataEntryBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "DataEntryBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCustomers_CreatedByUserId",
                table: "CollectionCustomers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryBatches_BatchNumber",
                table: "DataEntryBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryBatches_OrganizationId_Status_CreatedAt",
                table: "DataEntryBatches",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryBatches_PortfolioId",
                table: "DataEntryBatches",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryBatches_ReviewedByUserId",
                table: "DataEntryBatches",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryBatches_UploadedByUserId_CreatedAt",
                table: "DataEntryBatches",
                columns: new[] { "UploadedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryNotifications_BatchId_RecipientUserId_Kind",
                table: "DataEntryNotifications",
                columns: new[] { "BatchId", "RecipientUserId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryNotifications_RecipientUserId_IsRead_CreatedAt",
                table: "DataEntryNotifications",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryRows_BatchId_RowNumber",
                table: "DataEntryRows",
                columns: new[] { "BatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryRows_CollectionCaseId",
                table: "DataEntryRows",
                column: "CollectionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DataEntryRows_CollectionCustomerId",
                table: "DataEntryRows",
                column: "CollectionCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionCustomers_Users_CreatedByUserId",
                table: "CollectionCustomers",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionCustomers_Users_CreatedByUserId",
                table: "CollectionCustomers");

            migrationBuilder.DropTable(
                name: "DataEntryNotifications");

            migrationBuilder.DropTable(
                name: "DataEntryRows");

            migrationBuilder.DropTable(
                name: "DataEntryBatches");

            migrationBuilder.DropIndex(
                name: "IX_CollectionCustomers_CreatedByUserId",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "DataEntrySource",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CollectionCustomers");
        }
    }
}
