using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedReportSuggestionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AdId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AdName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CampaignId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CampaignName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AdsetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AdsetName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AggregateRoas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AggregateHookRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AggregateHoldRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AggregateSpend = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AggregatePurchases = table.Column<int>(type: "integer", nullable: true),
                    AnalyzedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_reports_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_report_suggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SavedReportId = table.Column<int>(type: "integer", nullable: false),
                    SuggestionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DirectiveType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Symptom = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Action = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    SkippedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    BeforeRoas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    BeforeHookRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    BeforeHoldRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    BeforeSpend = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BeforePurchases = table.Column<int>(type: "integer", nullable: true),
                    AfterRoas = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AfterHookRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AfterHoldRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    AfterSpend = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AfterPurchases = table.Column<int>(type: "integer", nullable: true),
                    ImpactMeasuredAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_report_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_report_suggestions_saved_reports_SavedReportId",
                        column: x => x.SavedReportId,
                        principalTable: "saved_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_report_suggestions_AppliedAt_ImpactMeasuredAt",
                table: "saved_report_suggestions",
                columns: new[] { "AppliedAt", "ImpactMeasuredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_saved_report_suggestions_SavedReportId_SuggestionKey",
                table: "saved_report_suggestions",
                columns: new[] { "SavedReportId", "SuggestionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_reports_UserId_AdId_AnalyzedAt",
                table: "saved_reports",
                columns: new[] { "UserId", "AdId", "AnalyzedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_report_suggestions");

            migrationBuilder.DropTable(
                name: "saved_reports");
        }
    }
}
