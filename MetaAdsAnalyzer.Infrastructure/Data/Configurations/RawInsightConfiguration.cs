using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class RawInsightConfiguration : IEntityTypeConfiguration<RawInsight>
{
    public void Configure(EntityTypeBuilder<RawInsight> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FetchedAt).HasPrecision(3);

        builder.HasIndex(e => new { e.UserId, e.Level, e.EntityId, e.DateStart, e.DateStop });

        builder.HasMany(e => e.ComputedMetrics)
            .WithOne(e => e.RawInsight)
            .HasForeignKey(e => e.RawInsightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
