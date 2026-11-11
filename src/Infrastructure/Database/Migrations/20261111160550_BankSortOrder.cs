using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class BankSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "banks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Keep each user's current order (default bank first, then by name).
            migrationBuilder.Sql(
                """
                UPDATE banks b SET "SortOrder" = o.rn - 1
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "UserId" ORDER BY "IsDefault" DESC, "Name") AS rn
                    FROM banks
                ) o
                WHERE b."Id" = o."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "banks");
        }
    }
}
