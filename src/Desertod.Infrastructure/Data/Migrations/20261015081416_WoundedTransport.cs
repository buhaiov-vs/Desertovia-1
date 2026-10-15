using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WoundedTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HospitalizedAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RescueByCitizenId",
                table: "Citizens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RescuePatientId",
                table: "Citizens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WoundedAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HospitalizedAt",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "RescueByCitizenId",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "RescuePatientId",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "WoundedAt",
                table: "Citizens");
        }
    }
}
