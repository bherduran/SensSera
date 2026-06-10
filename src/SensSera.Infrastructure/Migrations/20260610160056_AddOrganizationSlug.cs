using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensSera.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Organizations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Organizations");
        }
    }
}
