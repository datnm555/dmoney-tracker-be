using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePlaceRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchasePlaceId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchasePlaceId",
                table: "gold_acquisitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_PurchasePlaceId",
                table: "transactions",
                column: "PurchasePlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_gold_acquisitions_PurchasePlaceId",
                table: "gold_acquisitions",
                column: "PurchasePlaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_gold_acquisitions_purchase_places_PurchasePlaceId",
                table: "gold_acquisitions",
                column: "PurchasePlaceId",
                principalTable: "purchase_places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_purchase_places_PurchasePlaceId",
                table: "transactions",
                column: "PurchasePlaceId",
                principalTable: "purchase_places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gold_acquisitions_purchase_places_PurchasePlaceId",
                table: "gold_acquisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_purchase_places_PurchasePlaceId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_PurchasePlaceId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_gold_acquisitions_PurchasePlaceId",
                table: "gold_acquisitions");

            migrationBuilder.DropColumn(
                name: "PurchasePlaceId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "PurchasePlaceId",
                table: "gold_acquisitions");
        }
    }
}
