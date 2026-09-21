using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Badge = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "News",
                columns: new[] { "Id", "Badge", "Body", "Category", "Date", "Excerpt", "Title" },
                values: new object[,]
                {
                    { "n1", null, new[] { "Every Friday from 20:00 CET we play community 5v5s. More people in queue at the same time means shorter waits and closer matches." }, "event", new DateOnly(2026, 8, 29), "A standing night to find full stacks. Queue times drop, games get better.", "Weekly 5v5 night, Fridays at 20:00 CET" },
                    { "n2", null, new[] { "Every match played on FLICKED now records a demo automatically. Demos are stored on the server that hosted the match.", "Server owners can set how long demos are kept." }, "update", new DateOnly(2026, 9, 3), "Demos are saved on the server and can be downloaded from the match page.", "Every match now records a demo" },
                    { "n3", "0.1.3", new[] { "Declining a match now always returns you to the Play screen. Before, a declined match could leave you searching with no way to cancel.", "Reconnecting to a live match is faster, and the server log keeps the full match history." }, "patch", new DateOnly(2026, 9, 8), "Fixes a case where a declined match kept you in queue, plus smaller stability fixes.", "Alpha 0.1.3: queue fixes" },
                    { "n4", null, new[] { "Sign-ups for the first FLICKED Community Cup are open. Teams of five, single elimination, best of one until the final.", "Matches run on community-hosted servers. Brackets are published the day before the first round." }, "event", new DateOnly(2026, 9, 12), "Five-stack tournament, single elimination, played on community servers.", "Community Cup #1: sign-ups open" },
                    { "n5", null, new[] { "You do not need a cluster to run FLICKED. The new guide in the repository shows how to run the backend, PostgreSQL, Redis and a CS2 dedicated server on a single machine.", "It covers ports, the match config, and how to point the launcher at your own server." }, "update", new DateOnly(2026, 9, 15), "A new guide walks through running the backend, database and one CS2 server on one box.", "Self-host FLICKED on a single machine" },
                    { "n6", "0.2", new[] { "Parties are here. Invite friends from the list on the Play screen, or share your party code so they can join directly. The leader queues for everyone.", "Map veto is replaced by a map vote. After all ten players accept, everyone has 15 seconds to vote. The map with the most votes is played; a tie is settled at random.", "Also in this release: the launcher starts faster, fonts ship with the app, and the window no longer flashes white on open." }, "patch", new DateOnly(2026, 9, 18), "Queue with up to four friends, and pick the map together after everyone accepts.", "Alpha 0.2: parties and map vote" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "News");
        }
    }
}
