using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Map = table.Column<string>(type: "text", nullable: false),
                    ScoreA = table.Column<int>(type: "integer", nullable: false),
                    ScoreB = table.Column<int>(type: "integer", nullable: false),
                    PlayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayers",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Team = table.Column<int>(type: "integer", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    Adr = table.Column<int>(type: "integer", nullable: false),
                    RatingDelta = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayers", x => new { x.MatchId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Matches",
                columns: new[] { "Id", "Map", "PlayedAt", "ScoreA", "ScoreB" },
                values: new object[,]
                {
                    { 47903, "de_dust2", new DateTimeOffset(new DateTime(2026, 9, 18, 21, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 13, 10 },
                    { 47951, "de_anubis", new DateTimeOffset(new DateTime(2026, 9, 19, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 7, 13 },
                    { 48077, "de_ancient", new DateTimeOffset(new DateTime(2026, 9, 20, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 13, 11 },
                    { 48122, "de_nuke", new DateTimeOffset(new DateTime(2026, 9, 20, 21, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 13, 4 },
                    { 48190, "de_inferno", new DateTimeOffset(new DateTime(2026, 9, 21, 17, 5, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10, 13 },
                    { 48213, "de_mirage", new DateTimeOffset(new DateTime(2026, 9, 21, 18, 40, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 13, 9 }
                });

            migrationBuilder.InsertData(
                table: "MatchPlayers",
                columns: new[] { "MatchId", "PlayerId", "Adr", "Deaths", "Kills", "RatingDelta", "Team" },
                values: new object[,]
                {
                    { 47903, 1, 88, 14, 19, 20, 0 },
                    { 47951, 1, 61, 16, 11, -22, 0 },
                    { 48077, 1, 80, 15, 17, 18, 0 },
                    { 48122, 1, 91, 8, 18, 21, 0 },
                    { 48190, 1, 72, 17, 15, -19, 0 },
                    { 48213, 1, 97, 12, 21, 24, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_PlayerId",
                table: "MatchPlayers",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPlayers");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
