using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class TrackedCompetitorConfiguration : IEntityTypeConfiguration<TrackedCompetitor>
{
    public void Configure(EntityTypeBuilder<TrackedCompetitor> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAt).HasPrecision(3);
        builder.Property(x => x.UpdatedAt).HasPrecision(3);
        builder.Property(x => x.LastSyncedAt).HasPrecision(3);

        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasIndex(x => new { x.UserId, x.DisplayName });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Ads)
            .WithOne(x => x.TrackedCompetitor)
            .HasForeignKey(x => x.TrackedCompetitorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
