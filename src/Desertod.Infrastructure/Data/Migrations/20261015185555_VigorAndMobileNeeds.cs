using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VigorAndMobileNeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VigorCooldownMinutes",
                table: "Villages",
                type: "integer",
                nullable: false,
                defaultValue: 240);

            migrationBuilder.AddColumn<int>(
                name: "VigorEnergy",
                table: "Villages",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEnergyDrinkAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VigorCooldownMinutes",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "VigorEnergy",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "LastEnergyDrinkAt",
                table: "Citizens");
        }
    }
}
