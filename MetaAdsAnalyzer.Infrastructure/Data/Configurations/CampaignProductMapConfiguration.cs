using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class CampaignProductMapConfiguration : IEntityTypeConfiguration<CampaignProductMap>
{
    public void Configure(EntityTypeBuilder<CampaignProductMap> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.UserId, e.CampaignId }).IsUnique();
    }
}
