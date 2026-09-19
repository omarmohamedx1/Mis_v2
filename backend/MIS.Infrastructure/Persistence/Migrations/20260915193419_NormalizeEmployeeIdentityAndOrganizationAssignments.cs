using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEmployeeIdentityAndOrganizationAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeOrganizationAssignments",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeOrganizationAssignments", x => new { x.EmployeeId, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_EmployeeOrganizationAssignments_CollectionClientOrganizatio~",
                        column: x => x.OrganizationId,
                        principalTable: "CollectionClientOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeOrganizationAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrganizationAssignments_OrganizationId_EmployeeId",
                table: "EmployeeOrganizationAssignments",
                columns: new[] { "OrganizationId", "EmployeeId" });

            // Populate the localized identity fields for all historical records.
            // PostgreSQL's Arabic Unicode ranges also cover Arabic presentation forms.
            migrationBuilder.Sql("""
                UPDATE "Employees"
                SET "FullNameArabic" = btrim("FullName")
                WHERE ("FullNameArabic" IS NULL OR btrim("FullNameArabic") = '')
                  AND "FullName" ~ '[؀-ۿﭐ-﷿]';

                UPDATE "Employees"
                SET "FullNameEnglish" = btrim("FullName")
                WHERE ("FullNameEnglish" IS NULL OR btrim("FullNameEnglish") = '')
                  AND "FullName" !~ '[؀-ۿﭐ-﷿]';
                """);

            // Historical imports used the client code or bank name as an HR department.
            // Preserve that as an explicit client assignment, then move the employee to Collections.
            migrationBuilder.Sql("""
                INSERT INTO "EmployeeOrganizationAssignments"
                    ("EmployeeId", "OrganizationId", "IsPrimary", "AssignedAt")
                SELECT e."Id", organization."Id", TRUE, CURRENT_TIMESTAMP
                FROM "Employees" e
                JOIN "Departments" legacy ON legacy."Id" = e."DepartmentId"
                JOIN "CollectionClientOrganizations" organization
                    ON organization."Code" = legacy."Code"
                    OR lower(btrim(organization."NameEnglish")) = lower(btrim(legacy."Name"))
                    OR (legacy."NameArabic" IS NOT NULL AND btrim(legacy."NameArabic") <> ''
                        AND organization."NameArabic" = legacy."NameArabic")
                ON CONFLICT ("EmployeeId", "OrganizationId") DO NOTHING;

                INSERT INTO "EmployeeOrganizationAssignments"
                    ("EmployeeId", "OrganizationId", "IsPrimary", "AssignedAt")
                SELECT DISTINCT users."EmployeeId", access."OrganizationId",
                    NOT EXISTS (
                        SELECT 1 FROM "EmployeeOrganizationAssignments" existing
                        WHERE existing."EmployeeId" = users."EmployeeId"
                    ),
                    CURRENT_TIMESTAMP
                FROM "Users" users
                JOIN "CollectionUserAccess" access ON access."UserId" = users."Id"
                WHERE users."EmployeeId" IS NOT NULL
                ON CONFLICT ("EmployeeId", "OrganizationId") DO NOTHING;

                UPDATE "Employees" employee
                SET "DepartmentId" = collections."Id", "UpdatedAt" = CURRENT_TIMESTAMP
                FROM "Departments" legacy, "Departments" collections
                WHERE employee."DepartmentId" = legacy."Id"
                  AND collections."Code" = 'COLLECTIONS'
                  AND (
                    EXISTS (
                        SELECT 1
                        FROM "CollectionClientOrganizations" organization
                        WHERE organization."Code" = legacy."Code"
                           OR lower(btrim(organization."NameEnglish")) = lower(btrim(legacy."Name"))
                           OR (legacy."NameArabic" IS NOT NULL AND btrim(legacy."NameArabic") <> ''
                               AND organization."NameArabic" = legacy."NameArabic")
                    )
                    OR lower(btrim(legacy."Name")) IN ('bank', 'banks')
                    OR btrim(coalesce(legacy."NameArabic", '')) IN ('بنك', 'البنك', 'البنوك')
                  );

                UPDATE "Departments"
                SET "IsActive" = FALSE, "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE EXISTS (
                    SELECT 1
                    FROM "CollectionClientOrganizations" organization
                    WHERE organization."Code" = "Departments"."Code"
                       OR lower(btrim(organization."NameEnglish")) = lower(btrim("Departments"."Name"))
                       OR ("Departments"."NameArabic" IS NOT NULL AND btrim("Departments"."NameArabic") <> ''
                           AND organization."NameArabic" = "Departments"."NameArabic")
                )
                OR lower(btrim("Name")) IN ('bank', 'banks')
                OR btrim(coalesce("NameArabic", '')) IN ('بنك', 'البنك', 'البنوك');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeOrganizationAssignments");
        }
    }
}
