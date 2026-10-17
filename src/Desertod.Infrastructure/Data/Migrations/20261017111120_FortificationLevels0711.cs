using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FortificationLevels0711 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add only the tier. Existing damage, coordinates, gate state and ordering are unchanged.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "Fortifications"=(
                    SELECT COALESCE(jsonb_agg(CASE WHEN COALESCE((item->>'Level')::int,0)>0 THEN item
                        ELSE item || jsonb_build_object('Level',1) END ORDER BY ord),'[]'::jsonb)
                    FROM jsonb_array_elements(COALESCE(v."Fortifications",'[]'::jsonb)) WITH ORDINALITY a(item,ord));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
