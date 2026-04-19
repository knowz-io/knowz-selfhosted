using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrichmentAttemptsAndActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiProcessingAttempts",
                table: "EnrichmentOutbox",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedProcessingAt",
                table: "EnrichmentOutbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnrichmentActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KnowledgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrichmentActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichmentActivityLogs_TenantId_KnowledgeId",
                table: "EnrichmentActivityLogs",
                columns: new[] { "TenantId", "KnowledgeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichmentActivityLogs_TenantId_StartedAt",
                table: "EnrichmentActivityLogs",
                columns: new[] { "TenantId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrichmentActivityLogs");

            migrationBuilder.DropColumn(
                name: "AiProcessingAttempts",
                table: "EnrichmentOutbox");

            migrationBuilder.DropColumn(
                name: "StartedProcessingAt",
                table: "EnrichmentOutbox");
        }
    }
}
