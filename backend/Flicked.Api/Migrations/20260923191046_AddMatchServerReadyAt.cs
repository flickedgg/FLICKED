using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchServerReadyAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ServerReadyAt",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 47903,
                column: "ServerReadyAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 47951,
                column: "ServerReadyAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48077,
                column: "ServerReadyAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48122,
                column: "ServerReadyAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48190,
                column: "ServerReadyAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Matches",
                keyColumn: "Id",
                keyValue: 48213,
                column: "ServerReadyAt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServerReadyAt",
                table: "Matches");
        }
    }
}
