using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionCaseImportProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "CollectionCustomers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "CollectionCustomers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TertiaryPhone",
                table: "CollectionCustomers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ActivationDate",
                table: "CollectionCases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardNumber",
                table: "CollectionCases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "CollectionCases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportRawJson",
                table: "CollectionCases",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportStatusText",
                table: "CollectionCases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPaymentAmount",
                table: "CollectionCases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastTransactionAmount",
                table: "CollectionCases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastTransactionDate",
                table: "CollectionCases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseAvailableLimit",
                table: "CollectionCases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "CollectionCases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_CardNumber",
                table: "CollectionCases",
                column: "CardNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionCases_CardNumber",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "City",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "TertiaryPhone",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "ActivationDate",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "ImportRawJson",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "ImportStatusText",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "LastPaymentAmount",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "LastTransactionAmount",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "LastTransactionDate",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "PurchaseAvailableLimit",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "CollectionCases");
        }
    }
}
