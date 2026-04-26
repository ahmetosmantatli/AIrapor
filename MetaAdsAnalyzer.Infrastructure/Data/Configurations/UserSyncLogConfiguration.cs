using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class UserSyncLogConfiguration : IEntityTypeConfiguration<UserSyncLog>
{
    public void Configure(EntityTypeBuilder<UserSyncLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UpdatedAt).HasPrecision(3);
        builder.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
