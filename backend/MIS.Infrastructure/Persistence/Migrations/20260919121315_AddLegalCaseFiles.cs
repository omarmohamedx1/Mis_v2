using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalCaseFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalCaseFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CourtName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CourtCaseNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LawyerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    NextHearingOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalCaseFiles", x => x.Id);
                    table.CheckConstraint("CK_LegalCaseFiles_Stage", "\"Stage\" IN ('INTAKE','NOTICE','COURT','HEARING','JUDGMENT','SETTLEMENT','RETURNED')");
                    table.ForeignKey(
                        name: "FK_LegalCaseFiles_CollectionCases_CollectionCaseId",
                        column: x => x.CollectionCaseId,
                        principalTable: "CollectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalCaseFiles_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalCaseActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Result = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HappenedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalCaseActions", x => x.Id);
                    table.CheckConstraint("CK_LegalCaseActions_ActionType", "\"ActionType\" IN ('NOTE','NOTICE','HEARING','JUDGMENT','SETTLEMENT','RETURN')");
                    table.ForeignKey(
                        name: "FK_LegalCaseActions_LegalCaseFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "LegalCaseFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LegalCaseActions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalCaseActions_CreatedByUserId",
                table: "LegalCaseActions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCaseActions_FileId_CreatedAt",
                table: "LegalCaseActions",
                columns: new[] { "FileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalCaseFiles_CollectionCaseId",
                table: "LegalCaseFiles",
                column: "CollectionCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalCaseFiles_NextHearingOn",
                table: "LegalCaseFiles",
                column: "NextHearingOn");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCaseFiles_ReceivedByUserId",
                table: "LegalCaseFiles",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCaseFiles_Stage",
                table: "LegalCaseFiles",
                column: "Stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalCaseActions");

            migrationBuilder.DropTable(
                name: "LegalCaseFiles");
        }
    }
}
