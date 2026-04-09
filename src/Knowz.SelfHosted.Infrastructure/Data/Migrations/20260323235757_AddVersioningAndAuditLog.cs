using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVersioningAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlatformData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitRepository",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastSyncCommitSha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FilePatterns = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlatformData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitRepository", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KnowledgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlatformData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaultSyncLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalVaultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteVaultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformApiUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LastSyncCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPullCursor = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPushCursor = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<int>(type: "int", nullable: false),
                    LastSyncError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SyncEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultSyncLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncTombstones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaultSyncLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Propagated = table.Column<bool>(type: "bit", nullable: false),
                    PropagatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncTombstones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncTombstones_VaultSyncLinks_VaultSyncLinkId",
                        column: x => x.VaultSyncLinkId,
                        principalTable: "VaultSyncLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_GitRepository_TenantId",
                table: "GitRepository",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GitRepository_VaultId",
                table: "GitRepository",
                column: "VaultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeVersions_KnowledgeId_VersionNumber",
                table: "KnowledgeVersions",
                columns: new[] { "KnowledgeId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeVersions_TenantId_KnowledgeId",
                table: "KnowledgeVersions",
                columns: new[] { "TenantId", "KnowledgeId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncTombstones_VaultSyncLinkId_EntityType_LocalEntityId",
                table: "SyncTombstones",
                columns: new[] { "VaultSyncLinkId", "EntityType", "LocalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncTombstones_VaultSyncLinkId_Propagated",
                table: "SyncTombstones",
                columns: new[] { "VaultSyncLinkId", "Propagated" });

            migrationBuilder.CreateIndex(
                name: "IX_VaultSyncLinks_LocalVaultId",
                table: "VaultSyncLinks",
                column: "LocalVaultId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "GitRepository");

            migrationBuilder.DropTable(
                name: "KnowledgeVersions");

            migrationBuilder.DropTable(
                name: "SyncTombstones");

            migrationBuilder.DropTable(
                name: "VaultSyncLinks");
        }
    }
}
