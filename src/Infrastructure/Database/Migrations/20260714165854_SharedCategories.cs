using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <summary>
    /// Categories and sub-categories become shared (no per-user ownership):
    /// per-user duplicates merge into one canonical row per code, ownership
    /// moves to CreatedBy/UpdatedBy usernames, and transactions plus
    /// sub-categories now reference categories by a real CategoryId FK.
    /// </summary>
    public partial class SharedCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "categories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "categories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "sub_categories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "sub_categories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "sub_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                -- Ownership becomes an audit trail of usernames.
                UPDATE categories c
                SET "CreatedBy" = u."Username", "UpdatedBy" = u."Username"
                FROM users u WHERE u."Id" = c."UserId";

                UPDATE sub_categories s
                SET "CreatedBy" = u."Username", "UpdatedBy" = u."Username"
                FROM users u WHERE u."Id" = s."UserId";

                -- Point transactions at the canonical category per code (the
                -- earliest row); user-created categories map to themselves.
                WITH canon AS (
                    SELECT DISTINCT ON ("Code") "Id" AS new_id, "Code"
                    FROM categories WHERE "Code" IS NOT NULL
                    ORDER BY "Code", "CreatedAt", "Id"
                ), map AS (
                    SELECT c."Id" AS old_id, COALESCE(n.new_id, c."Id") AS new_id
                    FROM categories c LEFT JOIN canon n ON n."Code" = c."Code"
                )
                UPDATE transactions t
                SET "CategoryId" = m.new_id
                FROM map m
                WHERE t."Category" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                  AND m.old_id = t."Category"::uuid;

                -- Rows still holding a legacy code (e.g. entertainment) land in "other".
                UPDATE transactions t
                SET "CategoryId" = (
                    SELECT "Id" FROM categories WHERE "Code" = 'other'
                    ORDER BY "CreatedAt", "Id" LIMIT 1)
                WHERE t."Category" IS NOT NULL AND t."CategoryId" IS NULL;

                -- Same canonical mapping for sub-categories.
                WITH canon AS (
                    SELECT DISTINCT ON ("Code") "Id" AS new_id, "Code"
                    FROM categories WHERE "Code" IS NOT NULL
                    ORDER BY "Code", "CreatedAt", "Id"
                ), map AS (
                    SELECT c."Id" AS old_id, COALESCE(n.new_id, c."Id") AS new_id
                    FROM categories c LEFT JOIN canon n ON n."Code" = c."Code"
                )
                UPDATE sub_categories s
                SET "CategoryId" = m.new_id
                FROM map m
                WHERE s."Category" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                  AND m.old_id = s."Category"::uuid;

                DELETE FROM sub_categories WHERE "CategoryId" IS NULL;

                -- Merge sub-categories that became duplicates (same parent + name).
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "CategoryId", "Name"
                               ORDER BY "CreatedAt", "Id") AS keep_id
                    FROM sub_categories
                )
                UPDATE transactions t
                SET "SubCategoryId" = r.keep_id
                FROM ranked r
                WHERE t."SubCategoryId" = r."Id" AND r."Id" <> r.keep_id;

                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "CategoryId", "Name"
                               ORDER BY "CreatedAt", "Id") AS keep_id
                    FROM sub_categories
                )
                DELETE FROM sub_categories s
                USING ranked r
                WHERE s."Id" = r."Id" AND r."Id" <> r.keep_id;

                -- At most one default per parent after the merge.
                UPDATE sub_categories
                SET "IsDefault" = false
                WHERE "IsDefault" AND "Id" NOT IN (
                    SELECT DISTINCT ON ("CategoryId") "Id"
                    FROM sub_categories WHERE "IsDefault"
                    ORDER BY "CategoryId", "CreatedAt", "Id");

                -- Drop the per-user duplicate categories.
                WITH canon AS (
                    SELECT DISTINCT ON ("Code") "Id" AS new_id, "Code"
                    FROM categories WHERE "Code" IS NOT NULL
                    ORDER BY "Code", "CreatedAt", "Id"
                )
                DELETE FROM categories c
                USING canon n
                WHERE n."Code" = c."Code" AND c."Id" <> n.new_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_categories_users_UserId",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "FK_sub_categories_users_UserId",
                table: "sub_categories");

            migrationBuilder.DropIndex(
                name: "IX_sub_categories_UserId_Category",
                table: "sub_categories");

            migrationBuilder.DropIndex(
                name: "IX_categories_UserId",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "sub_categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "sub_categories");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "sub_categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CategoryId",
                table: "transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_sub_categories_CategoryId",
                table: "sub_categories",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_sub_categories_categories_CategoryId",
                table: "sub_categories",
                column: "CategoryId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_categories_CategoryId",
                table: "transactions",
                column: "CategoryId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The merge deletes per-user duplicates, so the previous state
            // cannot be reconstructed.
            throw new NotSupportedException(
                "Reverting the shared-categories merge is not supported.");
        }
    }
}
