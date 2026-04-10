using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByUserIdToKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "KnowledgeItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_TenantId_CreatedByUserId",
                table: "KnowledgeItems",
                columns: new[] { "TenantId", "CreatedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_TenantId_CreatedByUserId",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "KnowledgeItems");
        }
    }
}
