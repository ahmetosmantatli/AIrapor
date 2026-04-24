using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetaAdsetIdToRawInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetaAdsetId",
                table: "raw_insights",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetaAdsetId",
                table: "raw_insights");
        }
    }
}
