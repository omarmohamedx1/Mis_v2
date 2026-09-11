using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePersonnelFileChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredDocumentCode",
                table: "EmployeeDocuments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "DocumentTypes" ("Id", "Name", "Code", "NameArabic", "Description", "RequiresExpiryDate", "IsActive", "CreatedAt") VALUES
                    ('30000000-0000-0000-0000-000000000001', 'Birth Certificate', 'BIRTH_CERTIFICATE', 'شهادة الميلاد', 'Required employee personnel-file document.', FALSE, TRUE, NOW()),
                    ('30000000-0000-0000-0000-000000000002', 'National ID Copy', 'NATIONAL_ID_COPY', 'صورة البطاقة', 'Required employee personnel-file document.', FALSE, TRUE, NOW()),
                    ('30000000-0000-0000-0000-000000000003', 'Military Exemption / Status', 'MILITARY_STATUS', 'ورق الإعفاء / موقف التجنيد', 'Required for male employee personnel files.', FALSE, TRUE, NOW())
                ON CONFLICT ("Code") DO UPDATE SET
                    "NameArabic" = EXCLUDED."NameArabic",
                    "RequiresExpiryDate" = FALSE,
                    "IsActive" = TRUE;

                UPDATE "DocumentTypes"
                SET "NameArabic" = 'شهادة التخرج', "RequiresExpiryDate" = FALSE, "IsActive" = TRUE
                WHERE "Code" = 'GRADUATION_CERTIFICATE';

                WITH ranked AS (
                    SELECT d."Id",
                           CASE t."Code"
                               WHEN 'BIRTH_CERTIFICATE' THEN 'BIRTH_CERTIFICATE'
                               WHEN 'GRADUATION_CERTIFICATE' THEN 'GRADUATION_CERTIFICATE'
                               WHEN 'NATIONAL_ID' THEN 'NATIONAL_ID_COPY'
                               WHEN 'NATIONAL_ID_COPY' THEN 'NATIONAL_ID_COPY'
                               WHEN 'MILITARY_CERTIFICATE' THEN 'MILITARY_STATUS'
                               WHEN 'MILITARY_STATUS' THEN 'MILITARY_STATUS'
                           END AS required_code,
                           ROW_NUMBER() OVER (
                               PARTITION BY d."EmployeeId", CASE t."Code"
                                   WHEN 'BIRTH_CERTIFICATE' THEN 'BIRTH_CERTIFICATE'
                                   WHEN 'GRADUATION_CERTIFICATE' THEN 'GRADUATION_CERTIFICATE'
                                   WHEN 'NATIONAL_ID' THEN 'NATIONAL_ID_COPY'
                                   WHEN 'NATIONAL_ID_COPY' THEN 'NATIONAL_ID_COPY'
                                   WHEN 'MILITARY_CERTIFICATE' THEN 'MILITARY_STATUS'
                                   WHEN 'MILITARY_STATUS' THEN 'MILITARY_STATUS'
                               END
                               ORDER BY COALESCE(d."UpdatedAt", d."UploadedAt") DESC, d."Id"
                           ) AS version_rank
                    FROM "EmployeeDocuments" d
                    JOIN "DocumentTypes" t ON t."Id" = d."DocumentTypeId"
                    WHERE NOT d."IsDeleted" AND t."Code" IN
                        ('BIRTH_CERTIFICATE', 'GRADUATION_CERTIFICATE', 'NATIONAL_ID', 'NATIONAL_ID_COPY', 'MILITARY_CERTIFICATE', 'MILITARY_STATUS')
                )
                UPDATE "EmployeeDocuments" d
                SET "RequiredDocumentCode" = ranked.required_code
                FROM ranked
                WHERE d."Id" = ranked."Id" AND ranked.version_rank = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_RequiredDocumentCode",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "RequiredDocumentCode" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"RequiredDocumentCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_RequiredDocumentCode",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "RequiredDocumentCode",
                table: "EmployeeDocuments");
        }
    }
}
