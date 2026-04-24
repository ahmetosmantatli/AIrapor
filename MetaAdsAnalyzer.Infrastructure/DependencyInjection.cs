using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetaAdsAnalyzer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Set it in appsettings, user secrets, or environment variable ConnectionStrings__DefaultConnection.");
        }

        var connectionString = PostgreSqlConnectionStringNormalizer.Normalize(raw);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
