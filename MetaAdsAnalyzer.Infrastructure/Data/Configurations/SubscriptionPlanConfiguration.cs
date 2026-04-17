using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UpdatedAt).HasPrecision(3);
        builder.HasIndex(e => e.Code).IsUnique();

        var seedAt = new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero);
        builder.HasData(
            new SubscriptionPlan
            {
                Id = 1,
                Code = "standard",
                DisplayName = "Standart",
                Description = "Özet metrikler, kampanya eşlemesi, temel raporlar.",
                MonthlyPrice = 299m,
                Currency = "TRY",
                SortOrder = 1,
                IsActive = true,
                AllowsPdfExport = false,
                AllowsWatchlist = false,
                MaxLinkedMetaAdAccounts = 2,
                UpdatedAt = seedAt,
            },
            new SubscriptionPlan
            {
                Id = 2,
                Code = "pro",
                DisplayName = "Pro",
                Description = "PDF dışa aktarma, takip listesi ve genişletilmiş kullanım.",
                MonthlyPrice = 599m,
                Currency = "TRY",
                SortOrder = 2,
                IsActive = true,
                AllowsPdfExport = true,
                AllowsWatchlist = true,
                MaxLinkedMetaAdAccounts = 4,
                UpdatedAt = seedAt,
            });
    }
}
