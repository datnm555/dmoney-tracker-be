using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSubCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubCategoryId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sub_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sub_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sub_categories_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_SubCategoryId",
                table: "transactions",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_sub_categories_UserId_Category",
                table: "sub_categories",
                columns: new[] { "UserId", "Category" });

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_sub_categories_SubCategoryId",
                table: "transactions",
                column: "SubCategoryId",
                principalTable: "sub_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_sub_categories_SubCategoryId",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "sub_categories");

            migrationBuilder.DropIndex(
                name: "IX_transactions_SubCategoryId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "transactions");
        }
    }
}
