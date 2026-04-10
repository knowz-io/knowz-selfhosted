using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knowz.SelfHosted.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "InboxItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_TenantId_CreatedByUserId_CreatedAt",
                table: "InboxItems",
                columns: new[] { "TenantId", "CreatedByUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxItems_TenantId_CreatedByUserId_CreatedAt",
                table: "InboxItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "InboxItems");
        }
    }
}
