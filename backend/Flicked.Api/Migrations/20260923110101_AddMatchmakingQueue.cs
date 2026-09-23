using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchmakingQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "MatchPlayers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapVote",
                table: "MatchPlayers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Queue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Queue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Queue_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "MatchPlayers",
                keyColumns: new[] { "MatchId", "PlayerId" },
                keyValues: new object[] { 47903, 1 },
                columns: new[] { "AcceptedAt", "MapVote" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MatchPlayers",
                keyColumns: new[] { "MatchId", "PlayerId" },
                keyValues: new object[] { 47951, 1 },
                columns: new[] { "AcceptedAt", "MapVote" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MatchPlayers",
                keyColumns: new[] { "MatchId", "PlayerId" },
                keyValues: new object[] { 48077, 1 },
                columns: new[] { "AcceptedAt", "MapVote" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MatchPlayers",
                keyColumns: new[] { "MatchId", "PlayerId" },
                keyValues: new object[] { 48122, 1 },
                columns: new[] { "AcceptedAt", "MapVote" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MatchPlayers",
                keyColumns: new[] { "MatchId", "PlayerId" },
                keyValues: new object[] { 48190, 1 },
                columns: new[] { "AcceptedAt", "MapVote" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MatchPlayers",
                keyColumns: new[] { "MatchId", "PlayerId" },
                keyValues: new object[] { 48213, 1 },
                columns: new[] { "AcceptedAt", "MapVote" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Queue_Mode_JoinedAt",
                table: "Queue",
                columns: new[] { "Mode", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Queue_PlayerId",
                table: "Queue",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Queue");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "MapVote",
                table: "MatchPlayers");
        }
    }
}
