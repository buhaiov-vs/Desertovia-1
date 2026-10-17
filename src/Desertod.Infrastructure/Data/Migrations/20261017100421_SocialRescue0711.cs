using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SocialRescue0711 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TsarHospitalizedAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TsarRescueByCitizenId",
                table: "Villages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TsarRescueRequestedAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TsarRescueRewardEligible",
                table: "Villages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TsarWoundedAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RescueJourney",
                table: "Citizens",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RescueRequestedAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RescueRewardEligible",
                table: "Citizens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TsarHospitalizedAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "TsarRescueByCitizenId",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "TsarRescueRequestedAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "TsarRescueRewardEligible",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "TsarWoundedAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "RescueJourney",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "RescueRequestedAt",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "RescueRewardEligible",
                table: "Citizens");
        }
    }
}
