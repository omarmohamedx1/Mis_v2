using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkImportedCollectorsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileCollectorUserId",
                table: "CollectionCases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousCollectorUserId",
                table: "CollectionCases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_FileCollectorUserId",
                table: "CollectionCases",
                column: "FileCollectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCases_PreviousCollectorUserId",
                table: "CollectionCases",
                column: "PreviousCollectorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionCases_Users_FileCollectorUserId",
                table: "CollectionCases",
                column: "FileCollectorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionCases_Users_PreviousCollectorUserId",
                table: "CollectionCases",
                column: "PreviousCollectorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionCases_Users_FileCollectorUserId",
                table: "CollectionCases");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectionCases_Users_PreviousCollectorUserId",
                table: "CollectionCases");

            migrationBuilder.DropIndex(
                name: "IX_CollectionCases_FileCollectorUserId",
                table: "CollectionCases");

            migrationBuilder.DropIndex(
                name: "IX_CollectionCases_PreviousCollectorUserId",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "FileCollectorUserId",
                table: "CollectionCases");

            migrationBuilder.DropColumn(
                name: "PreviousCollectorUserId",
                table: "CollectionCases");
        }
    }
}
