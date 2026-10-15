using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class KingdomEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CyclePrivateOutput",
                table: "Citizens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PersonalGoods",
                table: "Citizens",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "MentionCitizenIds",
                table: "ChatMessages",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::uuid[]");

            // Preserve old profiles and JSON aggregates; only append missing new fields.
            migrationBuilder.Sql("""
                UPDATE "Citizens" SET "PersonalGoods" = '[]'::jsonb WHERE "PersonalGoods" IS NULL;
                ALTER TABLE "Citizens" ALTER COLUMN "PersonalGoods" SET DEFAULT '[]'::jsonb;
                UPDATE "Villages" SET "Storages" = COALESCE((SELECT jsonb_agg(item || jsonb_build_object(
                    'PersonalCosts', COALESCE(item->'PersonalCosts', '[]'::jsonb),
                    'OwnerAccountId', COALESCE(item->'OwnerAccountId', 'null'::jsonb)))
                    FROM jsonb_array_elements(COALESCE("Storages",'[]'::jsonb)) AS item), '[]'::jsonb);
                UPDATE "Villages" SET "Structures" = COALESCE((SELECT jsonb_agg(item || jsonb_build_object(
                    'OwnerAccountId', COALESCE(item->'OwnerAccountId', 'null'::jsonb)))
                    FROM jsonb_array_elements(COALESCE("Structures",'[]'::jsonb)) AS item), '[]'::jsonb);
                UPDATE "Villages" SET "Sites" = COALESCE((SELECT jsonb_agg(item || jsonb_build_object(
                    'OwnerAccountId', COALESCE(item->'OwnerAccountId', 'null'::jsonb)))
                    FROM jsonb_array_elements(COALESCE("Sites",'[]'::jsonb)) AS item), '[]'::jsonb);
                """);
            migrationBuilder.CreateTable(
                name: "EconomyReceipts",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomyReceipts", x => new { x.VillageId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_EconomyReceipts_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameWallets",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Coins = table.Column<long>(type: "bigint", nullable: false),
                    TestCandles = table.Column<long>(type: "bigint", nullable: false),
                    IncomeDay = table.Column<DateOnly>(type: "date", nullable: false),
                    EarnedToday = table.Column<long>(type: "bigint", nullable: false),
                    ProfitToday = table.Column<long>(type: "bigint", nullable: false),
                    ExchangeDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ExchangedToday = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameWallets", x => new { x.VillageId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_GameWallets_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameWallets_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KingdomEconomies",
                columns: table => new
                {
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TreasuryCoins = table.Column<long>(type: "bigint", nullable: false),
                    FundDay = table.Column<DateOnly>(type: "date", nullable: false),
                    CandlesIssuedToday = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KingdomEconomies", x => x.VillageId);
                    table.ForeignKey(
                        name: "FK_KingdomEconomies_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Asset = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Delta = table.Column<long>(type: "bigint", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    CostBasis = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BuyerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketLots_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameWallets_AccountId",
                table: "GameWallets",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_VillageId_At_Id",
                table: "LedgerEntries",
                columns: new[] { "VillageId", "At", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_VillageId_OperationId",
                table: "LedgerEntries",
                columns: new[] { "VillageId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketLots_VillageId_Status_CreatedAt",
                table: "MarketLots",
                columns: new[] { "VillageId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomyReceipts");

            migrationBuilder.DropTable(
                name: "GameWallets");

            migrationBuilder.DropTable(
                name: "KingdomEconomies");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "MarketLots");

            migrationBuilder.DropColumn(
                name: "CyclePrivateOutput",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "PersonalGoods",
                table: "Citizens");

            migrationBuilder.DropColumn(
                name: "MentionCitizenIds",
                table: "ChatMessages");
        }
    }
}
