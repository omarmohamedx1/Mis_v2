using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MIS.Infrastructure.Persistence;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261110090000_SoftDeleteFinanceClaims")]
public partial class SoftDeleteFinanceClaims : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DeletedById",
            schema: "finance",
            table: "claims",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAt",
            schema: "finance",
            table: "claims",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_claims_DeletedById",
            schema: "finance",
            table: "claims",
            column: "DeletedById");

        migrationBuilder.AddForeignKey(
            name: "FK_claims_Users_DeletedById",
            schema: "finance",
            table: "claims",
            column: "DeletedById",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_claims_Users_DeletedById", schema: "finance", table: "claims");
        migrationBuilder.DropIndex(name: "IX_claims_DeletedById", schema: "finance", table: "claims");
        migrationBuilder.DropColumn(name: "DeletedById", schema: "finance", table: "claims");
        migrationBuilder.DropColumn(name: "DeletedAt", schema: "finance", table: "claims");
    }
}
