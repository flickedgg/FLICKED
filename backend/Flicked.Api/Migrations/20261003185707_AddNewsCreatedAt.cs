using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "News",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "News",
                keyColumn: "Id",
                keyValue: "n1",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "News",
                keyColumn: "Id",
                keyValue: "n2",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "News",
                keyColumn: "Id",
                keyValue: "n3",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "News",
                keyColumn: "Id",
                keyValue: "n4",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "News",
                keyColumn: "Id",
                keyValue: "n5",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "News",
                keyColumn: "Id",
                keyValue: "n6",
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "News");
        }
    }
}
