using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MIS.Infrastructure.Persistence;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260918180000_AddCollectionFileDeskFields")]
    public partial class AddCollectionFileDeskFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecondaryAddress",
                table: "CollectionCustomers",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousCollectorName",
                table: "CollectionCases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileCollectorName",
                table: "CollectionCases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportBucketLabel",
                table: "CollectionCases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCustomers_NationalId",
                table: "CollectionCustomers",
                column: "NationalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionCustomers_NationalId",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "SecondaryAddress",
                table: "CollectionCustomers");

            migrationBuilder.DropColumn(
                name: "PreviousCollectorName",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "FileCollectorName",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "ImportBucketLabel",
                table: "CollectionCases");
        }
    }
}
