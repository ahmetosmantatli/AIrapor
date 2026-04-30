using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class CompetitorAdConfiguration : IEntityTypeConfiguration<CompetitorAd>
{
    public void Configure(EntityTypeBuilder<CompetitorAd> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeliveryStartTime).HasPrecision(3);
        builder.Property(x => x.DeliveryStopTime).HasPrecision(3);
        builder.Property(x => x.FirstSeenAt).HasPrecision(3);
        builder.Property(x => x.LastSeenAt).HasPrecision(3);

        builder.HasIndex(x => new { x.TrackedCompetitorId, x.MetaAdArchiveId }).IsUnique();
        builder.HasIndex(x => new { x.TrackedCompetitorId, x.LastSeenAt });
        builder.HasIndex(x => new { x.TrackedCompetitorId, x.IsActive });
        builder.HasIndex(x => x.PageId);
    }
}
