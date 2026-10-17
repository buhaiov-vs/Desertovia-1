using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuildingDisplayNumbers0711 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildingDisplayNumbers",
                table: "Villages",
                type: "jsonb",
                nullable: true);
            // Separate display registry. Never rewrite global Number, BuildingSequence, plots or contents.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "BuildingDisplayNumbers"=(
                    WITH source AS (
                        SELECT item->>'Number' AS number, item->>'Kind' AS kind
                        FROM jsonb_array_elements(COALESCE(v."Structures",'[]'::jsonb) || COALESCE(v."Storages",'[]'::jsonb) || COALESCE(v."Sites",'[]'::jsonb)) item
                    ), identities AS (
                        SELECT DISTINCT ON (number::int) number::int AS number,
                            CASE WHEN kind IN ('WoodPile','Woodyard') THEN 'Woodyard'
                                 WHEN kind IN ('StonePile','StoneYard') THEN 'StoneYard'
                                 WHEN kind IN ('DryingRack','Granary') THEN 'Granary'
                                 WHEN kind IN ('TownHall','Storehouse') THEN 'Storehouse' ELSE kind END AS family
                        FROM source WHERE COALESCE(number,'0')::int>0 ORDER BY number::int
                    ), labels AS (
                        SELECT number,family,row_number() OVER(PARTITION BY family ORDER BY number)::int AS value FROM identities
                    ) SELECT COALESCE(jsonb_agg(jsonb_build_object('GlobalNumber',number,'Family',family,'Value',value) ORDER BY number),'[]'::jsonb) FROM labels
                );
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingDisplayNumbers",
                table: "Villages");
        }
    }
}
