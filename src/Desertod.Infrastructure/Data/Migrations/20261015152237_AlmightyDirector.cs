using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlmightyDirector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing raids predate the owned route collection; preserve every old field and supply only the missing array.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "Raids" = (
                    SELECT jsonb_agg(CASE WHEN r ? 'ApproachPath' THEN r ELSE r || '{"ApproachPath":[]}'::jsonb END)
                    FROM jsonb_array_elements(v."Raids") r)
                WHERE v."Raids" IS NOT NULL AND jsonb_array_length(v."Raids") > 0
                  AND EXISTS (SELECT 1 FROM jsonb_array_elements(v."Raids") r WHERE NOT (r ? 'ApproachPath'));
                """);
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DefenseReadyAt",
                table: "Citizens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseSector",
                table: "Citizens",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlmightyObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    Option = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlmightyObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlmightyObservations_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlmightyProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Option = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Intent = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NoticeState = table.Column<string>(type: "text", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlmightyProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlmightyProposals_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlmightyStates",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Intent = table.Column<string>(type: "text", nullable: false),
                    NextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RestUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Lease = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastNoticeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSignal = table.Column<string>(type: "text", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: false),
                    BudgetDay = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CallsToday = table.Column<int>(type: "integer", nullable: false),
                    LastSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlmightyStates", x => x.VillageId);
                    table.ForeignKey(
                        name: "FK_AlmightyStates_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlmightyObservations_VillageId_At",
                table: "AlmightyObservations",
                columns: new[] { "VillageId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AlmightyProposals_ReviewId",
                table: "AlmightyProposals",
                column: "ReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlmightyProposals_VillageId_Status_CreatedAt",
                table: "AlmightyProposals",
                columns: new[] { "VillageId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlmightyStates_NextReviewAt",
                table: "AlmightyStates",
                column: "NextReviewAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlmightyObservations");

            migrationBuilder.DropTable(
                name: "AlmightyProposals");

            migrationBuilder.DropTable(
                name: "AlmightyStates");

            migrationBuilder.DropColumn(
                name: "DefenseReadyAt",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "DefenseSector",
                table: "Citizens");
        }
    }
}
