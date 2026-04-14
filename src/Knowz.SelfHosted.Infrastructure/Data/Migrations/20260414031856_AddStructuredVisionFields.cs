using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredVisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentAIProvider",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutDataJson",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TextExtractedAt",
                table: "FileRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextExtractionError",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TextExtractionStatus",
                table: "FileRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisionAnalyzedAt",
                table: "FileRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionExtractedText",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionObjectsJson",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionTagsJson",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentAIProvider",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "LayoutDataJson",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "TextExtractedAt",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "TextExtractionError",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "TextExtractionStatus",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "VisionAnalyzedAt",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "VisionExtractedText",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "VisionObjectsJson",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "VisionTagsJson",
                table: "FileRecords");
        }
    }
}
