using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgressionAndMeals0711 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One explicit default in the existing research ledger; no discovery or contribution is changed.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "Research"=(
                    SELECT COALESCE(jsonb_agg(CASE WHEN item ? 'KnowledgeWork' THEN item
                        ELSE item || jsonb_build_object('KnowledgeWork',0) END ORDER BY ord),'[]'::jsonb)
                    FROM jsonb_array_elements(COALESCE(v."Research",'[]'::jsonb)) WITH ORDINALITY a(item,ord));
                """);
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMealAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MealKind",
                table: "Citizens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MealUntil",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMealAt",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "MealKind",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "MealUntil",
                table: "Citizens");
        }
    }
}
