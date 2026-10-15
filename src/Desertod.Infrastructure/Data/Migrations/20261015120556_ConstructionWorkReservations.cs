using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConstructionWorkReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservedProjectId",
                table: "Citizens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedSiteWork",
                table: "Citizens",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedProjectId",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "ReservedSiteWork",
                table: "Citizens");
        }
    }
}
