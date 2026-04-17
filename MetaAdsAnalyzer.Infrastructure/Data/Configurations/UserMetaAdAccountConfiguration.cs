using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class UserMetaAdAccountConfiguration : IEntityTypeConfiguration<UserMetaAdAccount>
{
    public void Configure(EntityTypeBuilder<UserMetaAdAccount> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.LinkedAt).HasPrecision(3);

        builder.HasIndex(e => new { e.UserId, e.MetaAdAccountId }).IsUnique();

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserMetaAdAccounts)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
