using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensSera.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThresholdAndRefreshTokenIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing databases may already hold duplicate thresholds for a (greenhouse, metric) pair,
            // which would make the unique index fail and block startup (migrations run on start).
            // Keep the oldest of each pair; drop the others and their alerts (RESTRICT FK).
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "GreenhouseId", "Metric" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Thresholds"
                )
                DELETE FROM "Alerts" WHERE "ThresholdId" IN (SELECT "Id" FROM ranked WHERE rn > 1);

                WITH ranked AS (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "GreenhouseId", "Metric" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Thresholds"
                )
                DELETE FROM "Thresholds" WHERE "Id" IN (SELECT "Id" FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Thresholds_GreenhouseId",
                table: "Thresholds");

            migrationBuilder.CreateIndex(
                name: "IX_Thresholds_GreenhouseId_Metric",
                table: "Thresholds",
                columns: new[] { "GreenhouseId", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Thresholds_GreenhouseId_Metric",
                table: "Thresholds");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_Thresholds_GreenhouseId",
                table: "Thresholds",
                column: "GreenhouseId");
        }
    }
}
