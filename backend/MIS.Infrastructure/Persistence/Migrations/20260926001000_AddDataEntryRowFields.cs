using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataEntryRowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldsJson",
                table: "DataEntryRows",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldsJson",
                table: "DataEntryRows");
        }
    }
}
