using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    MetaAdAccountId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AttributionWindow = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "directives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DirectiveType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_directives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_directives_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Cogs = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShippingCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PaymentFeePct = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    ReturnRatePct = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    LtvMultiplier = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TargetMarginPct = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "raw_insights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DateStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DateStop = table.Column<DateOnly>(type: "date", nullable: false),
                    Spend = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Reach = table.Column<long>(type: "bigint", nullable: false),
                    Frequency = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    LinkClicks = table.Column<long>(type: "bigint", nullable: false),
                    CtrLink = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    CtrAll = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Cpm = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    CpcLink = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Purchases = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AddToCart = table.Column<long>(type: "bigint", nullable: false),
                    InitiateCheckout = table.Column<long>(type: "bigint", nullable: false),
                    ViewContent = table.Column<long>(type: "bigint", nullable: false),
                    VideoPlay3s = table.Column<long>(type: "bigint", nullable: false),
                    VideoThruplay = table.Column<long>(type: "bigint", nullable: false),
                    VideoP25 = table.Column<long>(type: "bigint", nullable: false),
                    VideoP50 = table.Column<long>(type: "bigint", nullable: false),
                    VideoP75 = table.Column<long>(type: "bigint", nullable: false),
                    VideoP100 = table.Column<long>(type: "bigint", nullable: false),
                    VideoAvgWatchTime = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_insights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_raw_insights_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_product_map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_product_map", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_product_map_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_campaign_product_map_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "computed_metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RawInsightId = table.Column<int>(type: "int", nullable: false),
                    Roas = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Cpa = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BreakEvenRoas = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    TargetRoas = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MaxCpa = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TargetCpa = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    HookRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    HoldRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    NetProfitPerOrder = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    NetMarginPct = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MismatchRatio = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_computed_metrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_computed_metrics_raw_insights_RawInsightId",
                        column: x => x.RawInsightId,
                        principalTable: "raw_insights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_product_map_ProductId",
                table: "campaign_product_map",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_product_map_UserId_CampaignId",
                table: "campaign_product_map",
                columns: new[] { "UserId", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_computed_metrics_RawInsightId",
                table: "computed_metrics",
                column: "RawInsightId");

            migrationBuilder.CreateIndex(
                name: "IX_directives_UserId_EntityId_EntityType_IsActive",
                table: "directives",
                columns: new[] { "UserId", "EntityId", "EntityType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_products_UserId",
                table: "products",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_raw_insights_UserId_Level_EntityId_DateStart_DateStop",
                table: "raw_insights",
                columns: new[] { "UserId", "Level", "EntityId", "DateStart", "DateStop" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_product_map");

            migrationBuilder.DropTable(
                name: "computed_metrics");

            migrationBuilder.DropTable(
                name: "directives");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "raw_insights");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
