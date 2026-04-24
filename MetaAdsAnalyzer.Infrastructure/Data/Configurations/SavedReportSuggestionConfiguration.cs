using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class SavedReportSuggestionConfiguration : IEntityTypeConfiguration<SavedReportSuggestion>
{
    public void Configure(EntityTypeBuilder<SavedReportSuggestion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AppliedAt).HasPrecision(3);
        builder.Property(x => x.SkippedAt).HasPrecision(3);
        builder.Property(x => x.ImpactMeasuredAt).HasPrecision(3);
        builder.HasIndex(x => new { x.SavedReportId, x.SuggestionKey }).IsUnique();
        builder.HasIndex(x => new { x.AppliedAt, x.ImpactMeasuredAt });
    }
}

