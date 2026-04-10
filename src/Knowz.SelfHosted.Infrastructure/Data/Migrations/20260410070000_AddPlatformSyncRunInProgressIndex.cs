using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSyncRunInProgressIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Filtered index accelerates the "is this tenant already running something?" check
            // used by the concurrency guard + rate limiter without paying the cost of a full scan
            // once the audit table grows. The filter keeps it tiny — only in-flight rows live here.
            migrationBuilder.CreateIndex(
                name: "IX_PlatformSyncRuns_TenantId_InProgress",
                table: "PlatformSyncRuns",
                column: "TenantId",
                filter: "[CompletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformSyncRuns_TenantId_InProgress",
                table: "PlatformSyncRuns");
        }
    }
}
