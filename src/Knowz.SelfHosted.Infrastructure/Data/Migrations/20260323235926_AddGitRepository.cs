using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GitRepository",
                table: "GitRepository");

            migrationBuilder.RenameTable(
                name: "GitRepository",
                newName: "GitRepositories");

            migrationBuilder.RenameIndex(
                name: "IX_GitRepository_VaultId",
                table: "GitRepositories",
                newName: "IX_GitRepositories_VaultId");

            migrationBuilder.RenameIndex(
                name: "IX_GitRepository_TenantId",
                table: "GitRepositories",
                newName: "IX_GitRepositories_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GitRepositories",
                table: "GitRepositories",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GitRepositories",
                table: "GitRepositories");

            migrationBuilder.RenameTable(
                name: "GitRepositories",
                newName: "GitRepository");

            migrationBuilder.RenameIndex(
                name: "IX_GitRepositories_VaultId",
                table: "GitRepository",
                newName: "IX_GitRepository_VaultId");

            migrationBuilder.RenameIndex(
                name: "IX_GitRepositories_TenantId",
                table: "GitRepository",
                newName: "IX_GitRepository_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GitRepository",
                table: "GitRepository",
                column: "Id");
        }
    }
}
