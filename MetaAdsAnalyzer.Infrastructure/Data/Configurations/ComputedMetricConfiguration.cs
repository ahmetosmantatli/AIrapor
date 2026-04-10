using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class ComputedMetricConfiguration : IEntityTypeConfiguration<ComputedMetric>
{
    public void Configure(EntityTypeBuilder<ComputedMetric> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ComputedAt).HasPrecision(3);
    }
}
