using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensSera.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNodeLastRejection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastRejectReason",
                table: "Nodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRejectedAt",
                table: "Nodes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRejectReason",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "LastRejectedAt",
                table: "Nodes");
        }
    }
}
