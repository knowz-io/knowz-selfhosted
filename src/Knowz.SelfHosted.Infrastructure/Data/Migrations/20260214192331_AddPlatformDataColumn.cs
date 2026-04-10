using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformDataColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "Vaults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "Topics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "Tags",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "Persons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "Locations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "KnowledgeItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "InboxItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformData",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "Vaults");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "InboxItems");

            migrationBuilder.DropColumn(
                name: "PlatformData",
                table: "Events");
        }
    }
}
