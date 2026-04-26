using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    public partial class AddRawInsightAttributionWindow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributionWindow",
                table: "raw_insights",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "7d_click_1d_view");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributionWindow",
                table: "raw_insights");
        }
    }
}
