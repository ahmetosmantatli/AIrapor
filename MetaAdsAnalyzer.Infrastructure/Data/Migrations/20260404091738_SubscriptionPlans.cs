using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_Code",
                table: "subscription_plans",
                column: "Code",
                unique: true);

            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[] { "Code", "DisplayName", "Description", "MonthlyPrice", "Currency", "SortOrder", "IsActive", "UpdatedAt" },
                values: new object[,]
                {
                    {
                        "standard",
                        "Standart",
                        "Özet metrikler, kampanya eşlemesi, temel raporlar.",
                        299m,
                        "TRY",
                        1,
                        true,
                        new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero)
                    },
                    {
                        "pro",
                        "Pro",
                        "PDF dışa aktarma, takip listesi ve genişletilmiş kullanım.",
                        599m,
                        "TRY",
                        2,
                        true,
                        new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero)
                    }
                });

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPlanId",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_users_SubscriptionPlanId",
                table: "users",
                column: "SubscriptionPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_subscription_plans_SubscriptionPlanId",
                table: "users",
                column: "SubscriptionPlanId",
                principalTable: "subscription_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_subscription_plans_SubscriptionPlanId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_SubscriptionPlanId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "users");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
