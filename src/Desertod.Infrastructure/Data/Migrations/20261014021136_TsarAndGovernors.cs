using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TsarAndGovernors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GovernorTrip",
                table: "Villages",
                newName: "TsarTrip");

            migrationBuilder.RenameColumn(
                name: "GovernorSpotY",
                table: "Villages",
                newName: "TsarSpotY");

            migrationBuilder.RenameColumn(
                name: "GovernorSpotX",
                table: "Villages",
                newName: "TsarSpotX");

            migrationBuilder.AddColumn<bool>(
                name: "IsGovernor",
                table: "Citizens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGovernor",
                table: "Citizens");

            migrationBuilder.RenameColumn(
                name: "TsarTrip",
                table: "Villages",
                newName: "GovernorTrip");

            migrationBuilder.RenameColumn(
                name: "TsarSpotY",
                table: "Villages",
                newName: "GovernorSpotY");

            migrationBuilder.RenameColumn(
                name: "TsarSpotX",
                table: "Villages",
                newName: "GovernorSpotX");
        }
    }
}
