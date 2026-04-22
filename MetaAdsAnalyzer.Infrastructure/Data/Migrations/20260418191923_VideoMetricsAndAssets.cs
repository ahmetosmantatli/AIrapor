using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VideoMetricsAndAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Video15Sec",
                table: "raw_insights",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Video30Sec",
                table: "raw_insights",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "VideoP95",
                table: "raw_insights",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "CompletionRatePct",
                table: "computed_metrics",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreativeScoreTotal",
                table: "computed_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ThumbstopRatePct",
                table: "computed_metrics",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Video15SecPerSpend",
                table: "computed_metrics",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Video3SecPerSpend",
                table: "computed_metrics",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ad_video_links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MetaAdAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VideoId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AdName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_video_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_video_links_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MetaAdAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VideoId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RepresentativeAdName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FirstSeenDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastSeenDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalSpend = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalPurchases = table.Column<long>(type: "bigint", nullable: false),
                    TotalPurchaseValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalRoas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    HookRateAvg = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    HoldRateAvg = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    CompletionRateAvg = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AggregatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_video_assets_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ad_video_links_UserId_MetaAdAccountId_AdId",
                table: "ad_video_links",
                columns: new[] { "UserId", "MetaAdAccountId", "AdId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_assets_UserId_MetaAdAccountId_VideoId",
                table: "video_assets",
                columns: new[] { "UserId", "MetaAdAccountId", "VideoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_video_links");

            migrationBuilder.DropTable(
                name: "video_assets");

            migrationBuilder.DropColumn(
                name: "Video15Sec",
                table: "raw_insights");

            migrationBuilder.DropColumn(
                name: "Video30Sec",
                table: "raw_insights");

            migrationBuilder.DropColumn(
                name: "VideoP95",
                table: "raw_insights");

            migrationBuilder.DropColumn(
                name: "CompletionRatePct",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "CreativeScoreTotal",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "ThumbstopRatePct",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "Video15SecPerSpend",
                table: "computed_metrics");

            migrationBuilder.DropColumn(
                name: "Video3SecPerSpend",
                table: "computed_metrics");
        }
    }
}
