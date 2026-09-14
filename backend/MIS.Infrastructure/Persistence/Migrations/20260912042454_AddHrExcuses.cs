using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHrExcuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HrExcuseMissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    FromTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ToTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceCollectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceChanged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrExcuseMissions", x => x.Id);
                    table.CheckConstraint("CK_HrMission_Source", "(\"SourceType\" = 'FieldVisit' AND \"SourceVisitId\" IS NOT NULL) OR (\"SourceType\" = 'Manual' AND \"SourceVisitId\" IS NULL)");
                    table.CheckConstraint("CK_HrMission_Status", "\"Status\" IN ('PendingApproval','Approved','Rejected','Cancelled')");
                    table.CheckConstraint("CK_HrMission_Times", "\"ToTime\" IS NULL OR (\"FromTime\" IS NOT NULL AND \"ToTime\" > \"FromTime\")");
                    table.ForeignKey(
                        name: "FK_HrExcuseMissions_CollectionFieldVisits_SourceVisitId",
                        column: x => x.SourceVisitId,
                        principalTable: "CollectionFieldVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrExcuseMissions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrExcuseMissions_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrExcuseMissions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrExcuseMissions_Users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrExcuseMissions_Users_SourceCollectorId",
                        column: x => x.SourceCollectorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrExcuseAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExcuseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrExcuseAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrExcuseAttachments_HrExcuseMissions_ExcuseId",
                        column: x => x.ExcuseId,
                        principalTable: "HrExcuseMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrExcuseAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseAttachments_ExcuseId",
                table: "HrExcuseAttachments",
                column: "ExcuseId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseAttachments_UploadedByUserId",
                table: "HrExcuseAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseMissions_ApprovedByUserId",
                table: "HrExcuseMissions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseMissions_CreatedByUserId",
                table: "HrExcuseMissions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseMissions_EmployeeId_Date_Status",
                table: "HrExcuseMissions",
                columns: new[] { "EmployeeId", "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseMissions_RejectedByUserId",
                table: "HrExcuseMissions",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseMissions_SourceCollectorId",
                table: "HrExcuseMissions",
                column: "SourceCollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExcuseMissions_SourceVisitId",
                table: "HrExcuseMissions",
                column: "SourceVisitId",
                unique: true,
                filter: "\"SourceVisitId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Employees_EmployeeId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "HrExcuseAttachments");

            migrationBuilder.DropTable(
                name: "HrExcuseMissions");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Users");
        }
    }
}
