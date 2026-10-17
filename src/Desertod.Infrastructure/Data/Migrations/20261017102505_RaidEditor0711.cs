using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RaidEditor0711 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RaidAdaptation",
                table: "Villages",
                type: "jsonb",
                nullable: false,
                defaultValue: "{\"Coefficient\":1,\"EasyStreak\":0,\"Reason\":\"Начальная сложность\",\"Recent\":[],\"Participation\":[]}");

            migrationBuilder.AddColumn<string>(
                name: "RaidPresets",
                table: "Villages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "RaidBattles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstContactAt",
                table: "RaidBattles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatedBy",
                table: "RaidBattles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastContactAt",
                table: "RaidBattles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManualOverride",
                table: "RaidBattles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "PlayerHealthLost",
                table: "RaidBattles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "RemediesUsed",
                table: "RaidBattles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "RaidBattles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings",
                table: "RaidBattles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RaidLaunchReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidLaunchReceipts", x => new { x.VillageId, x.Id });
                    table.ForeignKey(
                        name: "FK_RaidLaunchReceipts_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidLaunchReceipts");

            migrationBuilder.DropColumn(
                name: "RaidAdaptation",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "RaidPresets",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "FirstContactAt",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "InitiatedBy",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "LastContactAt",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "ManualOverride",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "PlayerHealthLost",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "RemediesUsed",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "RaidBattles");

            migrationBuilder.DropColumn(
                name: "Settings",
                table: "RaidBattles");
        }
    }
}
