using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkContextAndBriefSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BriefSummary",
                table: "KnowledgeItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextSummary",
                table: "ContentChunks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsContextualEmbedding",
                table: "ContentChunks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BriefSummary",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "ContextSummary",
                table: "ContentChunks");

            migrationBuilder.DropColumn(
                name: "IsContextualEmbedding",
                table: "ContentChunks");
        }
    }
}
