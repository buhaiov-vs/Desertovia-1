using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Combat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRaidAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Raids",
                table: "Villages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Villages" SET "Raids" = '[]' WHERE "Raids" IS NULL;""");

            migrationBuilder.AddColumn<long>(
                name: "RaidsPlannedPeriod",
                table: "Villages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Armor",
                table: "Citizens",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Arrows",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InjuredUntil",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weapon",
                table: "Citizens",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Villages_NextRaidAt",
                table: "Villages",
                column: "NextRaidAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Villages_NextRaidAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "NextRaidAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "Raids",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "RaidsPlannedPeriod",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "Armor",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "Arrows",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "InjuredUntil",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "Weapon",
                table: "Citizens");
        }
    }
}
