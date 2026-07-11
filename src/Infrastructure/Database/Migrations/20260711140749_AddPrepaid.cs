using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrepaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrepaid",
                table: "transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrepaidFrom",
                table: "transactions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrepaidTo",
                table: "transactions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrepaidTransactionId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_PrepaidTransactionId",
                table: "transactions",
                column: "PrepaidTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_transactions_PrepaidTransactionId",
                table: "transactions",
                column: "PrepaidTransactionId",
                principalTable: "transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_transactions_PrepaidTransactionId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_PrepaidTransactionId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "IsPrepaid",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "PrepaidFrom",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "PrepaidTo",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "PrepaidTransactionId",
                table: "transactions");
        }
    }
}
