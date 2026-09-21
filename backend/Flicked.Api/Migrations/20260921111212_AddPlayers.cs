using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "Losses", "Name", "Rating", "Wins" },
                values: new object[,]
                {
                    { 1, 146, "kovac", 2614, 311 },
                    { 2, 148, "Halden", 2571, 287 },
                    { 3, 148, "Nyx", 2498, 264 },
                    { 4, 147, "sprayz", 2402, 240 },
                    { 5, 127, "quietus", 2366, 198 },
                    { 6, 149, "reload", 2291, 215 },
                    { 7, 122, "Brine", 2240, 176 },
                    { 8, 122, "m0th", 2187, 169 },
                    { 9, 119, "lowground", 2105, 151 },
                    { 10, 117, "patchnote", 2050, 143 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
