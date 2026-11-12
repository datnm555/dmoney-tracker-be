using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <summary>
    /// Data only. Tags the sub-categories the transaction form picks for money moved between the
    /// owner's accounts: "Chuyển giữa tài khoản" (account_transfer, under the excluded "Chuyển tiền
    /// nội bộ") and "Trả thẻ tín dụng" (card_payment, under "Nợ"), creating each under its parent when
    /// missing. Savings become assets: the savings category ("Tích luỹ") is excluded from stats.
    /// A no-op on databases without those categories (e.g. fresh test databases).
    /// </summary>
    public partial class LinkStatsRules : Migration
    {
        /// <summary>Data step, public so an integration test can replay it against seeded data.</summary>
        public const string SeedSql = """
            UPDATE sub_categories
            SET "Code" = 'account_transfer'
            WHERE "Id" = (SELECT s."Id" FROM sub_categories s
                          WHERE s."Name" = 'Chuyển giữa tài khoản' AND s."Code" IS NULL
                          ORDER BY s."CreatedAt", s."Id" LIMIT 1)
              AND NOT EXISTS (SELECT 1 FROM sub_categories x WHERE x."Code" = 'account_transfer');

            INSERT INTO sub_categories
                ("Id", "CategoryId", "Name", "IsDefault", "Icon", "Code", "CreatedBy", "UpdatedBy", "CreatedAt", "ModifiedAt", "ExcludeFromStats")
            SELECT gen_random_uuid(), c."Id", 'Chuyển giữa tài khoản', false, NULL, 'account_transfer', 'system', 'system', now(), now(), false
            FROM categories c
            WHERE c."Name" = 'Chuyển tiền nội bộ'
              AND NOT EXISTS (SELECT 1 FROM sub_categories x WHERE x."Code" = 'account_transfer')
            ORDER BY c."CreatedAt", c."Id"
            LIMIT 1;

            UPDATE sub_categories
            SET "Code" = 'card_payment'
            WHERE "Id" = (SELECT s."Id" FROM sub_categories s
                          WHERE s."Name" = 'Trả thẻ tín dụng' AND s."Code" IS NULL
                          ORDER BY s."CreatedAt", s."Id" LIMIT 1)
              AND NOT EXISTS (SELECT 1 FROM sub_categories x WHERE x."Code" = 'card_payment');

            INSERT INTO sub_categories
                ("Id", "CategoryId", "Name", "IsDefault", "Icon", "Code", "CreatedBy", "UpdatedBy", "CreatedAt", "ModifiedAt", "ExcludeFromStats")
            SELECT gen_random_uuid(), c."Id", 'Trả thẻ tín dụng', false, NULL, 'card_payment', 'system', 'system', now(), now(), false
            FROM categories c
            WHERE c."Name" = 'Nợ'
              AND NOT EXISTS (SELECT 1 FROM sub_categories x WHERE x."Code" = 'card_payment')
            ORDER BY c."CreatedAt", c."Id"
            LIMIT 1;

            UPDATE categories SET "ExcludeFromStats" = true WHERE "Code" = 'savings';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SeedSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE sub_categories SET "Code" = NULL WHERE "Code" IN ('account_transfer', 'card_payment');
                UPDATE categories SET "ExcludeFromStats" = false WHERE "Code" = 'savings';
                """);
        }
    }
}
