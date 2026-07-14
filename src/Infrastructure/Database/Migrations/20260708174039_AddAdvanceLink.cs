using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvanceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdvanceTransactionId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AdvanceTransactionId",
                table: "transactions",
                column: "AdvanceTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_transactions_AdvanceTransactionId",
                table: "transactions",
                column: "AdvanceTransactionId",
                principalTable: "transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_transactions_AdvanceTransactionId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_AdvanceTransactionId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "AdvanceTransactionId",
                table: "transactions");
        }
    }
}
