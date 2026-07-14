using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReverseAdvanceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_transactions_AdvanceTransactionId",
                table: "transactions");

            migrationBuilder.RenameColumn(
                name: "AdvanceTransactionId",
                table: "transactions",
                newName: "ReimbursedByTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_AdvanceTransactionId",
                table: "transactions",
                newName: "IX_transactions_ReimbursedByTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_transactions_ReimbursedByTransactionId",
                table: "transactions",
                column: "ReimbursedByTransactionId",
                principalTable: "transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // The link direction flipped: old data had the CREDIT pointing at the
            // advance; move each pointer onto the advance row instead.
            migrationBuilder.Sql("""
                UPDATE transactions a
                SET "ReimbursedByTransactionId" = c."Id"
                FROM transactions c
                WHERE c."ReimbursedByTransactionId" = a."Id"
                  AND a."IsAdvance" = true
                  AND c."IsAdvance" = false;

                UPDATE transactions c
                SET "ReimbursedByTransactionId" = NULL
                WHERE c."IsAdvance" = false
                  AND c."ReimbursedByTransactionId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_transactions_ReimbursedByTransactionId",
                table: "transactions");

            migrationBuilder.RenameColumn(
                name: "ReimbursedByTransactionId",
                table: "transactions",
                newName: "AdvanceTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_ReimbursedByTransactionId",
                table: "transactions",
                newName: "IX_transactions_AdvanceTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_transactions_AdvanceTransactionId",
                table: "transactions",
                column: "AdvanceTransactionId",
                principalTable: "transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
