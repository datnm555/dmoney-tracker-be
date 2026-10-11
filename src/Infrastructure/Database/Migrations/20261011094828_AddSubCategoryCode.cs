using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <summary>
    /// Sub-categories gain a built-in Code. Data step: tag the existing "Sổ tiết kiệm"
    /// under the savings category as savings_book, or create it if missing. A no-op on
    /// databases without a savings category (e.g. fresh test databases).
    /// </summary>
    public partial class AddSubCategoryCode : Migration
    {
        /// <summary>Data step, public so an integration test can replay it against seeded data.</summary>
        public const string SeedSavingsBookSql = """
            UPDATE sub_categories s
            SET "Code" = 'savings_book'
            FROM categories c
            WHERE c."Id" = s."CategoryId"
              AND c."Code" = 'savings'
              AND s."Name" = 'Sổ tiết kiệm'
              AND s."Code" IS NULL
              AND NOT EXISTS (SELECT 1 FROM sub_categories x WHERE x."Code" = 'savings_book');

            INSERT INTO sub_categories
                ("Id", "CategoryId", "Name", "IsDefault", "Icon", "Code", "CreatedBy", "UpdatedBy", "CreatedAt", "ModifiedAt")
            SELECT gen_random_uuid(), c."Id", 'Sổ tiết kiệm', false, NULL, 'savings_book', 'system', 'system', now(), now()
            FROM categories c
            WHERE c."Code" = 'savings'
              AND NOT EXISTS (SELECT 1 FROM sub_categories x WHERE x."Code" = 'savings_book')
            ORDER BY c."CreatedAt", c."Id"
            LIMIT 1;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "sub_categories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(SeedSavingsBookSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "sub_categories");
        }
    }
}
