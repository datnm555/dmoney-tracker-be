using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "Time",
                table: "transactions",
                type: "time without time zone",
                nullable: true);

            // Existing rows: keep today's in-day order by taking the local (Vietnam) time they were entered.
            migrationBuilder.Sql(
                """
                UPDATE transactions
                SET "Time" = date_trunc('minute', ("CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh'))::time
                WHERE "Time" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Time",
                table: "transactions");
        }
    }
}
