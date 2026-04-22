using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class AdVideoLinkConfiguration : IEntityTypeConfiguration<AdVideoLink>
{
    public void Configure(EntityTypeBuilder<AdVideoLink> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UpdatedAt).HasPrecision(3);
        builder.HasIndex(e => new { e.UserId, e.MetaAdAccountId, e.AdId }).IsUnique();
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
