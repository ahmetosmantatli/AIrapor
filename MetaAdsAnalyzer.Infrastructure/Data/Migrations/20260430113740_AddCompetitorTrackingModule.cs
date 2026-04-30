using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitorTrackingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tracked_competitors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PageRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    LastSyncStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastSyncError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracked_competitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tracked_competitors_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "competitor_ads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrackedCompetitorId = table.Column<int>(type: "integer", nullable: false),
                    MetaAdArchiveId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PageName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: true),
                    TitleText = table.Column<string>(type: "text", nullable: true),
                    DescriptionText = table.Column<string>(type: "text", nullable: true),
                    SnapshotUrl = table.Column<string>(type: "text", nullable: true),
                    PublisherPlatforms = table.Column<string>(type: "text", nullable: true),
                    Languages = table.Column<string>(type: "text", nullable: true),
                    DeliveryStartTime = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DeliveryStopTime = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competitor_ads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_competitor_ads_tracked_competitors_TrackedCompetitorId",
                        column: x => x.TrackedCompetitorId,
                        principalTable: "tracked_competitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_competitor_ads_PageId",
                table: "competitor_ads",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_competitor_ads_TrackedCompetitorId_IsActive",
                table: "competitor_ads",
                columns: new[] { "TrackedCompetitorId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_competitor_ads_TrackedCompetitorId_LastSeenAt",
                table: "competitor_ads",
                columns: new[] { "TrackedCompetitorId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_competitor_ads_TrackedCompetitorId_MetaAdArchiveId",
                table: "competitor_ads",
                columns: new[] { "TrackedCompetitorId", "MetaAdArchiveId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracked_competitors_UserId_DisplayName",
                table: "tracked_competitors",
                columns: new[] { "UserId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_tracked_competitors_UserId_IsActive",
                table: "tracked_competitors",
                columns: new[] { "UserId", "IsActive" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competitor_ads");

            migrationBuilder.DropTable(
                name: "tracked_competitors");
        }
    }
}
