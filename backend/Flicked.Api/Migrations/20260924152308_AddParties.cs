using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Flicked.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeaderId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parties_Players_LeaderId",
                        column: x => x.LeaderId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartyId = table.Column<int>(type: "integer", nullable: false),
                    ToPlayerId = table.Column<int>(type: "integer", nullable: false),
                    FromPlayerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyInvites_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyInvites_Players_FromPlayerId",
                        column: x => x.FromPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyInvites_Players_ToPlayerId",
                        column: x => x.ToPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartyId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_LeaderId",
                table: "Parties",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_ExpiresAt",
                table: "PartyInvites",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_FromPlayerId",
                table: "PartyInvites",
                column: "FromPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_PartyId_ToPlayerId",
                table: "PartyInvites",
                columns: new[] { "PartyId", "ToPlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_ToPlayerId",
                table: "PartyInvites",
                column: "ToPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_PartyId",
                table: "PartyMembers",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_PlayerId",
                table: "PartyMembers",
                column: "PlayerId",
                unique: true);

            /* Everybody already waiting gets a party of one, so the next
               migration can point their queue entry at a party that exists.
               Written as one statement because the party ids are generated here
               and the members need them: Postgres hands them back from the
               INSERT rather than making this two passes over the queue. */
            migrationBuilder.Sql("""
                WITH made AS (
                    INSERT INTO "Parties" ("LeaderId", "CreatedAt")
                    SELECT q."PlayerId", now() FROM "Queue" q
                    RETURNING "Id", "LeaderId"
                )
                INSERT INTO "PartyMembers" ("PartyId", "PlayerId", "JoinedAt")
                SELECT made."Id", made."LeaderId", now() FROM made;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyInvites");

            migrationBuilder.DropTable(
                name: "PartyMembers");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
