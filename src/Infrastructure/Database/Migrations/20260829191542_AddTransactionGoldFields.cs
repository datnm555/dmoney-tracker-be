using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionGoldFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GoldQuantity",
                table: "transactions",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GoldTypeId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_GoldTypeId",
                table: "transactions",
                column: "GoldTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_gold_types_GoldTypeId",
                table: "transactions",
                column: "GoldTypeId",
                principalTable: "gold_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_gold_types_GoldTypeId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_GoldTypeId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "GoldQuantity",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "GoldTypeId",
                table: "transactions");
        }
    }
}
