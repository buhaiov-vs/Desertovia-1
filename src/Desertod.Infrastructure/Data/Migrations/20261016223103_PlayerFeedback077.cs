using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerFeedback077 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BuildingSequence",
                table: "Villages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentTargetPlotId",
                table: "Citizens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Backfill every existing JSON object without changing coordinates, owners, order, or contents.
            // An upgrade site shares its finished building's identity. Sequence never decreases after demolition.
            migrationBuilder.Sql("""
                WITH plots AS (
                    SELECT v."Id", item->>'PlotId' AS plot,
                           min(COALESCE(item->>'BuiltAt', item->>'PlacedAt', '')) AS created
                    FROM "Villages" v
                    CROSS JOIN LATERAL jsonb_array_elements(
                        COALESCE(v."Structures", '[]'::jsonb) || COALESCE(v."Storages", '[]'::jsonb) || COALESCE(v."Sites", '[]'::jsonb)) item
                    GROUP BY v."Id", item->>'PlotId'
                ), numbered AS (
                    SELECT "Id", plot, row_number() OVER (PARTITION BY "Id" ORDER BY created, plot COLLATE "C")::int AS number
                    FROM plots
                )
                UPDATE "Villages" v SET
                    "Structures" = (SELECT COALESCE(jsonb_agg(item || jsonb_build_object('Number', n.number) ORDER BY ord), '[]'::jsonb)
                        FROM jsonb_array_elements(COALESCE(v."Structures", '[]'::jsonb)) WITH ORDINALITY AS a(item, ord)
                        JOIN numbered n ON n."Id"=v."Id" AND n.plot=item->>'PlotId'),
                    "Storages" = (SELECT COALESCE(jsonb_agg(item || jsonb_build_object('Number', n.number) ORDER BY ord), '[]'::jsonb)
                        FROM jsonb_array_elements(COALESCE(v."Storages", '[]'::jsonb)) WITH ORDINALITY AS a(item, ord)
                        JOIN numbered n ON n."Id"=v."Id" AND n.plot=item->>'PlotId'),
                    "Sites" = (SELECT COALESCE(jsonb_agg(item || jsonb_build_object('Number', n.number) ORDER BY ord), '[]'::jsonb)
                        FROM jsonb_array_elements(COALESCE(v."Sites", '[]'::jsonb)) WITH ORDINALITY AS a(item, ord)
                        JOIN numbered n ON n."Id"=v."Id" AND n.plot=item->>'PlotId'),
                    "BuildingSequence" = (SELECT count(*)::int FROM numbered n WHERE n."Id"=v."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildingSequence",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "CurrentTargetPlotId",
                table: "Citizens");
        }
    }
}
