using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreatePostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsPdfExport = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsWatchlist = table.Column<bool>(type: "boolean", nullable: false),
                    MaxLinkedMetaAdAccounts = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    MetaAdAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MetaUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MetaAccessToken = table.Column<string>(type: "text", nullable: true),
                    MetaTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    Currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AttributionWindow = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanExpiresAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    StripeCustomerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_subscription_plans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "subscription_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "directives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DirectiveType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    HealthStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Cogs = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ShippingCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PaymentFeePct = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    ReturnRatePct = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    LtvMultiplier = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TargetMarginPct = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MetaCampaignId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetaAdAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DateStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DateStop = table.Column<DateOnly>(type: "date", nullable: false),
                    Spend = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Reach = table.Column<long>(type: "bigint", nullable: false),
                    Frequency = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    LinkClicks = table.Column<long>(type: "bigint", nullable: false),
                    CtrLink = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CtrAll = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Cpm = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CpcLink = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Purchases = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AddToCart = table.Column<long>(type: "bigint", nullable: false),
                    InitiateCheckout = table.Column<long>(type: "bigint", nullable: false),
                    ViewContent = table.Column<long>(type: "bigint", nullable: false),
                    VideoPlay3s = table.Column<long>(type: "bigint", nullable: false),
                    VideoThruplay = table.Column<long>(type: "bigint", nullable: false),
                    VideoP25 = table.Column<long>(type: "bigint", nullable: false),
                    VideoP50 = table.Column<long>(type: "bigint", nullable: false),
                    VideoP75 = table.Column<long>(type: "bigint", nullable: false),
                    VideoP100 = table.Column<long>(type: "bigint", nullable: false),
                    VideoAvgWatchTime = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
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
                name: "user_meta_ad_accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MetaAdAccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_meta_ad_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_meta_ad_accounts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "watchlist_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_watchlist_items_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_product_map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawInsightId = table.Column<int>(type: "integer", nullable: false),
                    Roas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    Cpa = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BreakEvenRoas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    TargetRoas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MaxCpa = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TargetCpa = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    HookRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    HoldRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    NetProfitPerOrder = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    NetMarginPct = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MismatchRatio = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
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

            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[] { "Id", "AllowsPdfExport", "AllowsWatchlist", "Code", "Currency", "Description", "DisplayName", "IsActive", "MaxLinkedMetaAdAccounts", "MonthlyPrice", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, false, false, "standard", "TRY", "Özet metrikler, kampanya eşlemesi, temel raporlar.", "Standart", true, 2, 299m, 1, new DateTimeOffset(new DateTime(2026, 4, 4, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 2, true, true, "pro", "TRY", "PDF dışa aktarma, takip listesi ve genişletilmiş kullanım.", "Pro", true, 4, 599m, 2, new DateTimeOffset(new DateTime(2026, 4, 4, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
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
                name: "IX_raw_insights_UserId_MetaAdAccountId_Level_EntityId_DateStar~",
                table: "raw_insights",
                columns: new[] { "UserId", "MetaAdAccountId", "Level", "EntityId", "DateStart", "DateStop" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_Code",
                table: "subscription_plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_meta_ad_accounts_UserId_MetaAdAccountId",
                table: "user_meta_ad_accounts",
                columns: new[] { "UserId", "MetaAdAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_MetaUserId",
                table: "users",
                column: "MetaUserId",
                unique: true,
                filter: "\"MetaUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_SubscriptionPlanId",
                table: "users",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_items_UserId_Level_EntityId",
                table: "watchlist_items",
                columns: new[] { "UserId", "Level", "EntityId" },
                unique: true);

            // Sabit Id ile InsertData sonrası bir sonraki otomatik Id çakışmasın.
            migrationBuilder.Sql(
                """
                SELECT setval(
                    pg_get_serial_sequence('subscription_plans', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM subscription_plans), 1));
                """);
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
                name: "user_meta_ad_accounts");

            migrationBuilder.DropTable(
                name: "watchlist_items");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "raw_insights");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
