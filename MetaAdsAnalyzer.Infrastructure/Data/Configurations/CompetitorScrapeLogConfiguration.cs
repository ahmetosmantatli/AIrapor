using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class CompetitorScrapeLogConfiguration : IEntityTypeConfiguration<CompetitorScrapeLog>
{
    public void Configure(EntityTypeBuilder<CompetitorScrapeLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedAt).HasPrecision(3);
        builder.Property(x => x.FinishedAt).HasPrecision(3);

        builder.HasIndex(x => new { x.UserId, x.StartedAt });
        builder.HasIndex(x => new { x.TrackedCompetitorId, x.StartedAt });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TrackedCompetitor)
            .WithMany()
            .HasForeignKey(x => x.TrackedCompetitorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
