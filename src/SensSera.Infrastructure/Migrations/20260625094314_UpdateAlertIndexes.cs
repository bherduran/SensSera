using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensSera.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAlertIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_OrganizationId_Status",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_OrganizationId_GreenhouseId",
                table: "Alerts",
                columns: new[] { "OrganizationId", "GreenhouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_OrganizationId_Status_TriggeredAt",
                table: "Alerts",
                columns: new[] { "OrganizationId", "Status", "TriggeredAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_OrganizationId_GreenhouseId",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_OrganizationId_Status_TriggeredAt",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_OrganizationId_Status",
                table: "Alerts",
                columns: new[] { "OrganizationId", "Status" });
        }
    }
}
