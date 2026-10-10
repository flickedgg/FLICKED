using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "Matches",
                type: "integer",
                nullable: true);

            /* Fill it in for the matches being played right now.

               A match that is live when this migration runs has no ServerId, and
               the server hosting it is about to need one: a result is only
               accepted from the server the match was sent to, and without this
               the real game in progress would be refused when it reported its
               score. The server still holding the match is the answer, which is
               exactly what CurrentMatchId says until the match ends.

               Nothing to do for matches already finished: they are rated, and
               the column is history from here on rather than something read
               about the past. */
            migrationBuilder.Sql("""
                UPDATE "Matches" m
                   SET "ServerId" = s."Id"
                  FROM "Servers" s
                 WHERE s."CurrentMatchId" = m."Id"
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ServerId",
                table: "Matches",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Servers_ServerId",
                table: "Matches",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Servers_ServerId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_ServerId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Matches");
        }
    }
}
