using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchConfigToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigTokenHash",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 47903,
                column: "ConfigTokenHash",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 47951,
                column: "ConfigTokenHash",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48077,
                column: "ConfigTokenHash",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48122,
                column: "ConfigTokenHash",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48190,
                column: "ConfigTokenHash",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48213,
                column: "ConfigTokenHash",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigTokenHash",
                table: "Matches");
        }
    }
}
