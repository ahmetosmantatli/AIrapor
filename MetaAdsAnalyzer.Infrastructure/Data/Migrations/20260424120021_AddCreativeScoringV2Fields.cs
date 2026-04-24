using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreativeScoringV2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreativeScoreColor",
                table: "computed_metrics",
                type: "character varying(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreativeScoreLabel",
                table: "computed_metrics",
                type: "character varying(32)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVideoCreative",
                table: "computed_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VideoMetricsUnavailable",
                table: "computed_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreativeScoreColor",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "CreativeScoreLabel",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "IsVideoCreative",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "VideoMetricsUnavailable",
                table: "computed_metrics");
        }
    }
}
