using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Adds commit-history ingestion fields to GitRepositories table.
    /// WorkGroupID: kc-feat-git-commit-knowledge-sync-20260410
    /// NodeID: SelfHostedCommitHistoryParity (NODE-4)
    /// </summary>
    public partial class AddGitRepositoryCommitHistoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrackCommitHistory",
                table: "GitRepositories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CommitHistoryDepth",
                table: "GitRepositories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastCommitHistorySyncSha",
                table: "GitRepositories",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCommitHistorySyncSha",
                table: "GitRepositories");

            migrationBuilder.DropColumn(
                name: "CommitHistoryDepth",
                table: "GitRepositories");

            migrationBuilder.DropColumn(
                name: "TrackCommitHistory",
                table: "GitRepositories");
        }
    }
}
