using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingPayrollAndCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingCommissionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Basis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingCommissionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountingPayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPayrollPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingPayrollPeriods_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingPayrollPeriods_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingTransportationClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FieldVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttachmentFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AttachmentStorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AttachmentLength = table.Column<long>(type: "bigint", nullable: true),
                    AttachmentSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingTransportationClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingTransportationClaims_CollectionCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CollectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingTransportationClaims_CollectionFieldVisits_FieldV~",
                        column: x => x.FieldVisitId,
                        principalTable: "CollectionFieldVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingTransportationClaims_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingTransportationClaims_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingTransportationClaims_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingCollectorCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    CollectorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedCasesCount = table.Column<int>(type: "integer", nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EligibleAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    RateApplied = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Adjustments = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalCommission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingCollectorCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingCollectorCommissions_AccountingCommissionRules_Co~",
                        column: x => x.CommissionRuleId,
                        principalTable: "AccountingCommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingCollectorCommissions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingCollectorCommissions_Users_CollectorUserId",
                        column: x => x.CollectorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingSupervisorCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    SupervisorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CollectorCount = table.Column<int>(type: "integer", nullable: false),
                    TeamCollectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EligibleAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    RateApplied = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Adjustments = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalCommission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingSupervisorCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingSupervisorCommissions_AccountingCommissionRules_C~",
                        column: x => x.CommissionRuleId,
                        principalTable: "AccountingCommissionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingSupervisorCommissions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingSupervisorCommissions_Users_SupervisorUserId",
                        column: x => x.SupervisorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingEmployeePayrolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PositionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompensationSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Allowances = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Transportation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Commissions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Bonuses = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Deductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherAdjustments = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEmployeePayrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingEmployeePayrolls_AccountingPayrollPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "AccountingPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEmployeePayrolls_EmployeeCompensations_Compensati~",
                        column: x => x.CompensationSnapshotId,
                        principalTable: "EmployeeCompensations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingEmployeePayrolls_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCollectorCommissions_CollectorUserId_PeriodYear_P~",
                table: "AccountingCollectorCommissions",
                columns: new[] { "CollectorUserId", "PeriodYear", "PeriodMonth" },
                unique: true,
                filter: "\"Status\" <> 'CANCELLED'");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCollectorCommissions_CommissionRuleId",
                table: "AccountingCollectorCommissions",
                column: "CommissionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCollectorCommissions_EmployeeId",
                table: "AccountingCollectorCommissions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCommissionRules_Code_Version",
                table: "AccountingCommissionRules",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCommissionRules_Scope_IsActive_EffectiveFrom",
                table: "AccountingCommissionRules",
                columns: new[] { "Scope", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEmployeePayrolls_CompensationSnapshotId",
                table: "AccountingEmployeePayrolls",
                column: "CompensationSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEmployeePayrolls_EmployeeId",
                table: "AccountingEmployeePayrolls",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEmployeePayrolls_PeriodId_EmployeeId",
                table: "AccountingEmployeePayrolls",
                columns: new[] { "PeriodId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPayrollPeriods_ApprovedByUserId",
                table: "AccountingPayrollPeriods",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPayrollPeriods_CreatedByUserId",
                table: "AccountingPayrollPeriods",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPayrollPeriods_Year_Month",
                table: "AccountingPayrollPeriods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSupervisorCommissions_CommissionRuleId",
                table: "AccountingSupervisorCommissions",
                column: "CommissionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSupervisorCommissions_EmployeeId",
                table: "AccountingSupervisorCommissions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSupervisorCommissions_SupervisorUserId_PeriodYear~",
                table: "AccountingSupervisorCommissions",
                columns: new[] { "SupervisorUserId", "PeriodYear", "PeriodMonth" },
                unique: true,
                filter: "\"Status\" <> 'CANCELLED'");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransportationClaims_ApprovedByUserId",
                table: "AccountingTransportationClaims",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransportationClaims_CaseId",
                table: "AccountingTransportationClaims",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransportationClaims_EmployeeId_ClaimDate",
                table: "AccountingTransportationClaims",
                columns: new[] { "EmployeeId", "ClaimDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransportationClaims_FieldVisitId",
                table: "AccountingTransportationClaims",
                column: "FieldVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransportationClaims_Status",
                table: "AccountingTransportationClaims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransportationClaims_SubmittedByUserId",
                table: "AccountingTransportationClaims",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingCollectorCommissions");

            migrationBuilder.DropTable(
                name: "AccountingEmployeePayrolls");

            migrationBuilder.DropTable(
                name: "AccountingSupervisorCommissions");

            migrationBuilder.DropTable(
                name: "AccountingTransportationClaims");

            migrationBuilder.DropTable(
                name: "AccountingPayrollPeriods");

            migrationBuilder.DropTable(
                name: "AccountingCommissionRules");
        }
    }
}
