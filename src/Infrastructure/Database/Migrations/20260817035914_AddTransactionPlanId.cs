using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionPlanId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE transactions t
                SET "PlanId" = p."Id"
                FROM plans p
                WHERE p."UserId" = t."UserId" AND p."IsDefault";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "PlanId",
                table: "transactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_PlanId",
                table: "transactions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_UserId_PlanId",
                table: "transactions",
                columns: new[] { "UserId", "PlanId" });

            migrationBuilder.AddForeignKey(
                name: "FK_plans_users_UserId",
                table: "plans",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_plans_PlanId",
                table: "transactions",
                column: "PlanId",
                principalTable: "plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_plans_users_UserId",
                table: "plans");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_plans_PlanId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_PlanId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_UserId_PlanId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "transactions");
        }
    }
}
