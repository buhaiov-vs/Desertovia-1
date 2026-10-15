using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CooperativeVillageUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BridgeOpened",
                table: "Villages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CivicOrders",
                table: "Villages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "BridgeHands",
                table: "VillageProjects",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.AddColumn<int>(
                name: "RewardedStages",
                table: "VillageProjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RulesVersion",
                table: "VillageProjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExhaustedCycles",
                table: "Citizens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExhaustedUntil",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomePlotId",
                table: "Citizens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarriageCheckedAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarriedSince",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpouseAccountId",
                table: "Citizens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseName",
                table: "Citizens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "ChaplainMessages",
                type: "text",
                nullable: true);

            // Preserve old bridges. Version 0 projects deliberately keep the original work requirements.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "BridgeOpened" = TRUE
                WHERE EXISTS (SELECT 1 FROM "VillageProjects" p WHERE p."VillageId"=v."Id"
                              AND p."DefinitionId"='tsar-bridge' AND p."Status"='Completed');
                UPDATE "Villages" SET "CivicOrders"='[]'::jsonb WHERE "CivicOrders" IS NULL;
                UPDATE "Villages" SET "Structures"=COALESCE((SELECT jsonb_agg(
                    '{"Level":1,"Decommissioning":false,"ProductionCycles":0,"Investment":[]}'::jsonb || item)
                    FROM jsonb_array_elements("Structures") item),'[]'::jsonb);
                UPDATE "Villages" SET "Storages"=COALESCE((SELECT jsonb_agg(
                    '{"Decommissioning":false,"Investment":[]}'::jsonb || item)
                    FROM jsonb_array_elements("Storages") item),'[]'::jsonb);
                UPDATE "Villages" SET "Sites"=COALESCE((SELECT jsonb_agg(
                    '{"Operation":null,"OrderId":null,"OperationWork":0}'::jsonb || item)
                    FROM jsonb_array_elements("Sites") item),'[]'::jsonb);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BridgeOpened",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "CivicOrders",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "BridgeHands",
                table: "VillageProjects");

            migrationBuilder.DropColumn(
                name: "RewardedStages",
                table: "VillageProjects");

            migrationBuilder.DropColumn(
                name: "RulesVersion",
                table: "VillageProjects");

            migrationBuilder.DropColumn(
                name: "ExhaustedCycles",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "ExhaustedUntil",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "HomePlotId",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "MarriageCheckedAt",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "MarriedSince",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "SpouseAccountId",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "SpouseName",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "ChaplainMessages");
        }
    }
}
