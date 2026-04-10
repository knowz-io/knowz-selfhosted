using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKnowledgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetKnowledgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    IsBidirectional = table.Column<bool>(type: "bit", nullable: false),
                    IsAutoDetected = table.Column<bool>(type: "bit", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PlatformData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeRelationships_KnowledgeItems_SourceKnowledgeId",
                        column: x => x.SourceKnowledgeId,
                        principalTable: "KnowledgeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeRelationships_KnowledgeItems_TargetKnowledgeId",
                        column: x => x.TargetKnowledgeId,
                        principalTable: "KnowledgeItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelationships_Source_Type",
                table: "KnowledgeRelationships",
                columns: new[] { "SourceKnowledgeId", "RelationshipType" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelationships_Target_Type",
                table: "KnowledgeRelationships",
                columns: new[] { "TargetKnowledgeId", "RelationshipType" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelationships_Tenant_Source_Target",
                table: "KnowledgeRelationships",
                columns: new[] { "TenantId", "SourceKnowledgeId", "TargetKnowledgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRelationships_TenantId",
                table: "KnowledgeRelationships",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeRelationships");
        }
    }
}
