using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetaChangeDetectionToSavedSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MetaChangeDetected",
                table: "saved_report_suggestions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MetaChangeMessage",
                table: "saved_report_suggestions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetaChangeDetected",
                table: "saved_report_suggestions");

            migrationBuilder.DropColumn(
                name: "MetaChangeMessage",
                table: "saved_report_suggestions");
        }
    }
}
