using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Post071ControlsAndSalvage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SiteRecoveries",
                table: "Villages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Villages" SET "SiteRecoveries"='[]'::jsonb;
                UPDATE "Villages" SET "Sites"=COALESCE((SELECT jsonb_agg(
                  '{"PlacedByAccountId":null,"BoundaryApproved":false,"PublicDemolition":false,"Salvage":[]}'::jsonb || site)
                  FROM jsonb_array_elements("Sites") site),'[]'::jsonb);
                UPDATE "RaidBattles" SET "Fighters"=COALESCE((SELECT jsonb_agg(
                  '{"AccountId":null,"IsTsar":false,"DestinationX":null,"DestinationY":null}'::jsonb || fighter)
                  FROM jsonb_array_elements("Fighters") fighter),'[]'::jsonb);
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TsarInjuredUntil",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS(SELECT 1 FROM "Villages" WHERE jsonb_array_length(COALESCE("SiteRecoveries",'[]'::jsonb))>0
                    OR "TsarInjuredUntil">now()
                    OR EXISTS(SELECT 1 FROM jsonb_array_elements("Sites") site WHERE site->>'Operation'='site-demolish'))
                    OR EXISTS(SELECT 1 FROM "RaidBattles" b CROSS JOIN LATERAL jsonb_array_elements(b."Fighters") f
                      WHERE b."FinishedAt" IS NULL AND f->>'IsTsar'='true')
                  THEN RAISE EXCEPTION 'Cannot roll back with pending salvage, dismantling, royal recovery or active royal combat'; END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "SiteRecoveries",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "TsarInjuredUntil",
                table: "Villages");
        }
    }
}
