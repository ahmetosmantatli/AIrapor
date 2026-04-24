using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AnalyzedAt).HasPrecision(3);
        builder.HasIndex(x => new { x.UserId, x.AdId, x.AnalyzedAt });
        builder.HasMany(x => x.Suggestions)
            .WithOne(x => x.SavedReport)
            .HasForeignKey(x => x.SavedReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

