using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialInsuranceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialInsuranceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocialInsuranceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InsuranceStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InsuranceEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InsurableSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InsuranceOffice = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialInsuranceRecords", x => x.Id);
                    table.CheckConstraint("CK_SocialInsurance_Dates", "\"InsuranceEndDate\" IS NULL OR \"InsuranceStartDate\" IS NULL OR \"InsuranceEndDate\" >= \"InsuranceStartDate\"");
                    table.CheckConstraint("CK_SocialInsurance_End", "(\"InsuranceStatus\" = 'Ended') = (\"InsuranceEndDate\" IS NOT NULL)");
                    table.CheckConstraint("CK_SocialInsurance_Salary", "\"InsurableSalary\" > 0");
                    table.CheckConstraint("CK_SocialInsurance_Start", "\"InsuranceStatus\" NOT IN ('Insured','Suspended') OR \"InsuranceStartDate\" IS NOT NULL");
                    table.CheckConstraint("CK_SocialInsurance_Status", "\"InsuranceStatus\" IN ('Insured','NotInsured','Suspended','Ended')");
                    table.ForeignKey(
                        name: "FK_SocialInsuranceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRecords_EmployeeId",
                table: "SocialInsuranceRecords",
                column: "EmployeeId",
                unique: true,
                filter: "\"InsuranceStatus\" <> 'Ended'");

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRecords_SocialInsuranceNumber",
                table: "SocialInsuranceRecords",
                column: "SocialInsuranceNumber",
                unique: true,
                filter: "\"InsuranceStatus\" <> 'Ended'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialInsuranceRecords");
        }
    }
}
