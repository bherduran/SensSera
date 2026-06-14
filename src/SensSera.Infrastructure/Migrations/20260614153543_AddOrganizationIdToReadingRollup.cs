using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensSera.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIdToReadingRollup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReadingRollups_DeviceId_Metric_Bucket_PeriodStart",
                table: "ReadingRollups");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ReadingRollups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ReadingRollups_DeviceId",
                table: "ReadingRollups",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingRollups_OrganizationId_DeviceId_Metric_Bucket_Period~",
                table: "ReadingRollups",
                columns: new[] { "OrganizationId", "DeviceId", "Metric", "Bucket", "PeriodStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReadingRollups_DeviceId",
                table: "ReadingRollups");

            migrationBuilder.DropIndex(
                name: "IX_ReadingRollups_OrganizationId_DeviceId_Metric_Bucket_Period~",
                table: "ReadingRollups");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ReadingRollups");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingRollups_DeviceId_Metric_Bucket_PeriodStart",
                table: "ReadingRollups",
                columns: new[] { "DeviceId", "Metric", "Bucket", "PeriodStart" },
                unique: true);
        }
    }
}
