using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryCodeAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "categories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // Seed the built-in categories for every existing user, then move
            // transactions and sub-categories from code strings to category ids.
            migrationBuilder.Sql("""
                INSERT INTO categories ("Id", "UserId", "Name", "Icon", "Code", "CreatedAt", "ModifiedAt")
                SELECT gen_random_uuid(), u."Id", v.name, v.icon, v.code, now(), now()
                FROM users u
                CROSS JOIN (VALUES
                    ('living', 'Sinh hoạt', 'house'),
                    ('salary', 'Lương', 'wallet'),
                    ('education', 'Tiền học', 'graduation-cap'),
                    ('food', 'Ăn hàng', 'utensils'),
                    ('shopping', 'Mua sắm', 'shopping-bag'),
                    ('bills', 'Hóa đơn', 'zap'),
                    ('savings', 'Tích luỹ', 'piggy-bank'),
                    ('other', 'Khác', 'tag')
                ) AS v(code, name, icon)
                WHERE NOT EXISTS (
                    SELECT 1 FROM categories c WHERE c."UserId" = u."Id" AND c."Code" = v.code);

                UPDATE transactions t
                SET "Category" = c."Id"::text
                FROM categories c
                WHERE c."UserId" = t."UserId" AND c."Code" = t."Category";

                UPDATE sub_categories s
                SET "Category" = c."Id"::text
                FROM categories c
                WHERE c."UserId" = s."UserId" AND c."Code" = s."Category";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Put code strings back on transactions and sub-categories, then
            // remove the seeded rows.
            migrationBuilder.Sql("""
                UPDATE transactions t
                SET "Category" = c."Code"
                FROM categories c
                WHERE c."Code" IS NOT NULL AND c."UserId" = t."UserId" AND t."Category" = c."Id"::text;

                UPDATE sub_categories s
                SET "Category" = c."Code"
                FROM categories c
                WHERE c."Code" IS NOT NULL AND c."UserId" = s."UserId" AND s."Category" = c."Id"::text;

                DELETE FROM categories WHERE "Code" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Code",
                table: "categories");
        }
    }
}
