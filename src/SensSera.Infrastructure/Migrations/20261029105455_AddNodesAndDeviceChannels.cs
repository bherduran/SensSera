using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensSera.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNodesAndDeviceChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NodeId",
                table: "Devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GreenhouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nodes_Greenhouses_GreenhouseId",
                        column: x => x.GreenhouseId,
                        principalTable: "Greenhouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_NodeId_Channel",
                table: "Devices",
                columns: new[] { "NodeId", "Channel" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Devices_NodeChannel",
                table: "Devices",
                sql: "(\"NodeId\" IS NULL) = (\"Channel\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_GreenhouseId",
                table: "Nodes",
                column: "GreenhouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Nodes_NodeId",
                table: "Devices",
                column: "NodeId",
                principalTable: "Nodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Nodes_NodeId",
                table: "Devices");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Devices_NodeId_Channel",
                table: "Devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Devices_NodeChannel",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NodeId",
                table: "Devices");
        }
    }
}
