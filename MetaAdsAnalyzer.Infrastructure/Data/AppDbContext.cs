using System.Reflection;
using MetaAdsAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<CampaignProductMap> CampaignProductMaps => Set<CampaignProductMap>();

    public DbSet<RawInsight> RawInsights => Set<RawInsight>();

    public DbSet<ComputedMetric> ComputedMetrics => Set<ComputedMetric>();

    public DbSet<Directive> Directives => Set<Directive>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<UserMetaAdAccount> UserMetaAdAccounts => Set<UserMetaAdAccount>();

    public DbSet<AdVideoLink> AdVideoLinks => Set<AdVideoLink>();

    public DbSet<VideoAsset> VideoAssets => Set<VideoAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
