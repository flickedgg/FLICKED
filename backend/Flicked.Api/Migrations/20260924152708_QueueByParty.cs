using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class QueueByParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Queue_Players_PlayerId",
                table: "Queue");

            migrationBuilder.RenameColumn(
                name: "PlayerId",
                table: "Queue",
                newName: "PartyId");

            migrationBuilder.RenameIndex(
                name: "IX_Queue_PlayerId",
                table: "Queue",
                newName: "IX_Queue_PartyId");

            /* The column has been renamed but still holds player ids. Every
               player who was waiting got a party of one in the previous
               migration, so each row is pointed at that party. Anything that
               still fails to map has no party to wait as and would break the
               foreign key, so it leaves the queue instead. */
            migrationBuilder.Sql("""
                UPDATE "Queue" q
                SET "PartyId" = m."PartyId"
                FROM "PartyMembers" m
                WHERE m."PlayerId" = q."PartyId";
                """);

            migrationBuilder.Sql("""
                DELETE FROM "Queue" q
                WHERE NOT EXISTS (SELECT 1 FROM "Parties" p WHERE p."Id" = q."PartyId");
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Queue_Parties_PartyId",
                table: "Queue",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Queue_Parties_PartyId",
                table: "Queue");

            migrationBuilder.RenameColumn(
                name: "PartyId",
                table: "Queue",
                newName: "PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_Queue_PartyId",
                table: "Queue",
                newName: "IX_Queue_PlayerId");

            /* Back to one row per player: the leader keeps the place, and a
               party of several loses the members it was holding a seat for. */
            migrationBuilder.Sql("""
                UPDATE "Queue" q
                SET "PlayerId" = p."LeaderId"
                FROM "Parties" p
                WHERE p."Id" = q."PlayerId";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Queue_Players_PlayerId",
                table: "Queue",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
