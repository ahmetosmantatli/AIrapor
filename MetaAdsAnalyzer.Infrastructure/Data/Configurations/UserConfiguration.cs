using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetaAdsAnalyzer.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedAt).HasPrecision(3);
        builder.Property(e => e.MetaTokenExpiresAt).HasPrecision(3);
        builder.Property(e => e.PlanExpiresAt).HasPrecision(3);
        builder.Property(e => e.SubscriptionStatus).HasMaxLength(32);
        builder.Property(e => e.StripeCustomerId).HasMaxLength(128);
        builder.Property(e => e.StripeSubscriptionId).HasMaxLength(128);
        builder.Property(e => e.MetaAccessToken).HasColumnType("text");
        builder.Property(e => e.PasswordHash).HasMaxLength(512);

        builder.HasIndex(e => e.Email).IsUnique();
        builder.HasIndex(e => e.MetaUserId)
            .IsUnique()
            .HasFilter("\"MetaUserId\" IS NOT NULL");

        builder.HasMany(e => e.Products)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.CampaignProductMaps)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.RawInsights)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Directives)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.WatchlistItems)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SubscriptionPlan)
            .WithMany(p => p.Users)
            .HasForeignKey(e => e.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
