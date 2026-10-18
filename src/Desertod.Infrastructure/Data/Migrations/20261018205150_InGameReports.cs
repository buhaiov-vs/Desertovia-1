using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InGameReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Screenshot = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerReports_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerReports_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_AccountId_CreatedAt",
                table: "PlayerReports",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_DeliveryState_NextAttemptAt",
                table: "PlayerReports",
                columns: new[] { "DeliveryState", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_VillageId",
                table: "PlayerReports",
                column: "VillageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerReports");
        }
    }
}
