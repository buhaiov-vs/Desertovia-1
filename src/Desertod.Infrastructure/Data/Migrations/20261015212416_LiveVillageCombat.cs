using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiveVillageCombat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fortifications",
                table: "Villages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RaidBattles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EngineVersion = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    Sector = table.Column<string>(type: "text", nullable: false),
                    Tactic = table.Column<string>(type: "text", nullable: false),
                    Composition = table.Column<string>(type: "text", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    ThreatBudget = table.Column<double>(type: "double precision", nullable: false),
                    ReadyDefenders = table.Column<int>(type: "integer", nullable: false),
                    AvailableReserves = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTickAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Deferred = table.Column<bool>(type: "boolean", nullable: false),
                    DamageDone = table.Column<double>(type: "double precision", nullable: false),
                    Damage = table.Column<string>(type: "jsonb", nullable: true),
                    Events = table.Column<string>(type: "jsonb", nullable: true),
                    Fighters = table.Column<string>(type: "jsonb", nullable: true),
                    Units = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidBattles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidBattles_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidBattles_VillageId_FinishedAt",
                table: "RaidBattles",
                columns: new[] { "VillageId", "FinishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "Fortifications",
                table: "Villages");
        }
    }
}
