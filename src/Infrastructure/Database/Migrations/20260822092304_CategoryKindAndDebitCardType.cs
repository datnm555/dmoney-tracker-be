using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CategoryKindAndDebitCardType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "categories",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "expense");

            // Income/both kinds for the categories that are not pure expenses.
            migrationBuilder.Sql(
                """UPDATE categories SET "Kind" = 'income' WHERE "Code" = 'salary';""");
            migrationBuilder.Sql(
                """UPDATE categories SET "Kind" = 'both' WHERE "Code" IN ('other', 'savings');""");

            // Card type codes: 'visa' (a network, not a kind) becomes 'debit'.
            migrationBuilder.Sql(
                """UPDATE transactions SET "CardType" = 'debit' WHERE "CardType" = 'visa';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "categories");
        }
    }
}
