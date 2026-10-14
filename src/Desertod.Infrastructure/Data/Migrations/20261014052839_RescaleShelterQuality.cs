using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RescaleShelterQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Citizens" SET "ShelterQuality" = 0.9 WHERE "ShelterQuality" > 0.7 AND "ShelterQuality" < 0.8;""");
            migrationBuilder.Sql("""UPDATE "Citizens" SET "ShelterQuality" = 0.8 WHERE "ShelterQuality" < 0.6;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Citizens" SET "ShelterQuality" = 0.5 WHERE "ShelterQuality" > 0.7 AND "ShelterQuality" < 0.85;""");
            migrationBuilder.Sql("""UPDATE "Citizens" SET "ShelterQuality" = 0.75 WHERE "ShelterQuality" > 0.85 AND "ShelterQuality" < 0.95;""");
        }
    }
}
