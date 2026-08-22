using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionBeneficiaryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BeneficiaryId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_BeneficiaryId",
                table: "transactions",
                column: "BeneficiaryId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_beneficiaries_BeneficiaryId",
                table: "transactions",
                column: "BeneficiaryId",
                principalTable: "beneficiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_beneficiaries_BeneficiaryId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_BeneficiaryId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "BeneficiaryId",
                table: "transactions");
        }
    }
}
