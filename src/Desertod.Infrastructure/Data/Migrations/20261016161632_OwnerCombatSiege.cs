using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnerCombatSiege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add only the required numeric default; preserve every existing site and its order.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "Sites" = (
                    SELECT COALESCE(jsonb_agg(s || jsonb_build_object('Damage', COALESCE((s->>'Damage')::int,0)) ORDER BY ordinal), '[]'::jsonb)
                    FROM jsonb_array_elements(COALESCE(v."Sites",'[]'::jsonb)) WITH ORDINALITY AS sites(s,ordinal)
                );
                """);
            migrationBuilder.AddColumn<int>(name:"BridgeDamage",table:"Villages",type:"integer",nullable:false,defaultValue:0);
            migrationBuilder.AddColumn<DateTimeOffset>(name:"BridgeRuinedAt",table:"Villages",type:"timestamp with time zone",nullable:true);
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DefeatedAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefeatedByBattleId",
                table: "Villages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name:"BridgeDamage",table:"Villages");
            migrationBuilder.DropColumn(name:"BridgeRuinedAt",table:"Villages");
            migrationBuilder.DropColumn(
                name: "DefeatedAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "DefeatedByBattleId",
                table: "Villages");
        }
    }
}
