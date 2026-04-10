using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class DirectiveConfiguration : IEntityTypeConfiguration<Directive>
{
    public void Configure(EntityTypeBuilder<Directive> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TriggeredAt).HasPrecision(3);

        builder.HasIndex(e => new { e.UserId, e.EntityId, e.EntityType, e.IsActive });
    }
}
