using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desertod.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CivicTelegramNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Сохраняем заявки целиком; старые pending получают очередь уведомлений.
            migrationBuilder.Sql("""
                UPDATE "Villages" v SET "CivicOrders" = (
                    SELECT COALESCE(jsonb_agg(o || jsonb_build_object(
                        'NoticeState', COALESCE(o->>'NoticeState', 'Queued'),
                        'NoticeAt', COALESCE(o->'NoticeAt', 'null'::jsonb)) ORDER BY n), '[]'::jsonb)
                    FROM jsonb_array_elements(COALESCE(v."CivicOrders", '[]'::jsonb)) WITH ORDINALITY AS a(o,n)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
