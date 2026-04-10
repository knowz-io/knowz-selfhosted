using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KnowledgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EmbeddingVectorJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmbeddedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PlatformData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentChunks_KnowledgeItems_KnowledgeId",
                        column: x => x.KnowledgeId,
                        principalTable: "KnowledgeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentChunks_KnowledgeId_ContentHash",
                table: "ContentChunks",
                columns: new[] { "KnowledgeId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentChunks_KnowledgeId_Position",
                table: "ContentChunks",
                columns: new[] { "KnowledgeId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentChunks_TenantId_KnowledgeId",
                table: "ContentChunks",
                columns: new[] { "TenantId", "KnowledgeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentChunks");
        }
    }
}
