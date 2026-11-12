using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ToAccountId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_ToAccountId",
                table: "transactions",
                column: "ToAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_accounts_ToAccountId",
                table: "transactions",
                column: "ToAccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_accounts_ToAccountId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_ToAccountId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "transactions");
        }
    }
}
