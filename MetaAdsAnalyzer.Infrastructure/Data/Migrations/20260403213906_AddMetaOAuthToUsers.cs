using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetaOAuthToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetaAccessToken",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MetaTokenExpiresAt",
                table: "users",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaUserId",
                table: "users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_MetaUserId",
                table: "users",
                column: "MetaUserId",
                unique: true,
                filter: "[MetaUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_MetaUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MetaAccessToken",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MetaTokenExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MetaUserId",
                table: "users");
        }
    }
}
