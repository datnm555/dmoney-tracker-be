using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringPaymentAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "recurring_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_payments_AccountId",
                table: "recurring_payments",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_recurring_payments_accounts_AccountId",
                table: "recurring_payments",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recurring_payments_accounts_AccountId",
                table: "recurring_payments");

            migrationBuilder.DropIndex(
                name: "IX_recurring_payments_AccountId",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "recurring_payments");
        }
    }
}
