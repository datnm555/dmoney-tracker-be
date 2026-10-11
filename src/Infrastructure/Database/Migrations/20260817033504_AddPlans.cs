using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plans_UserId",
                table: "plans",
                column: "UserId");

            // Seed the default plan for every existing user.
            migrationBuilder.Sql("""
                INSERT INTO plans ("Id", "UserId", "Name", "IsDefault", "CreatedAt", "ModifiedAt")
                SELECT gen_random_uuid(), u."Id", 'Sổ chính', TRUE, now(), now()
                FROM users u
                WHERE NOT EXISTS (
                    SELECT 1 FROM plans p WHERE p."UserId" = u."Id" AND p."IsDefault");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
