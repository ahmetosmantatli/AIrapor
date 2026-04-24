using System.Globalization;
using Npgsql;

namespace MetaAdsAnalyzer.Infrastructure.Data;

/// <summary>
/// Supabase ve benzeri servislerin verdiği <c>postgresql://...</c> URI'sini Npgsql'in anladığı
/// anahtar=değer bağlantı dizesine çevirir. Render vb. ortamlarda env değerindeki <c>sslmode=require</c>
/// içindeki <c>=</c> karakterinin kesilmesi sorununu da aşmak için tercih edilir.
/// </summary>
/// <remarks>
/// Render yalnızca IPv4 çıkışı kullanır; Supabase <b>doğrudan</b> <c>db.*.supabase.co</c> adresi çoğu projede
/// yalnızca IPv6’dır — bu durumda Dashboard → Connect → <b>Session pooler</b> (Supavisor, port 5432,
/// kullanıcı <c>postgres.PROJECT_REF</c>) dizesini kullanın. Ayrıntı:
/// https://supabase.com/docs/guides/database/connecting-to-postgres
/// </remarks>
public static class PostgreSqlConnectionStringNormalizer
{
    public static string Normalize(string connectionString)
    {
        var cs = connectionString.Trim();
        if (!cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return cs;
        }

        // System.Uri "postgresql" şemasını tanımayabilir; http ile aynı ayrıştırma modeli kullanılır.
        var tail = cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? cs["postgresql://".Length..]
            : cs["postgres://".Length..];
        if (!Uri.TryCreate($"http://{tail}", UriKind.Absolute, out var uri))
        {
            return cs;
        }

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        var database = uri.AbsolutePath.TrimStart('/').Trim();
        if (string.IsNullOrEmpty(database))
        {
            database = "postgres";
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.IdnHost,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
        };

        var sslFromQuery = ParseSslModeFromQuery(uri.Query);
        if (sslFromQuery.HasValue)
        {
            builder.SslMode = sslFromQuery.Value;
        }
        else if (builder.Host.Contains("supabase", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }

    private static SslMode? ParseSslModeFromQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        var q = query.StartsWith('?') ? query[1..] : query;
        foreach (var segment in q.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = segment.IndexOf('=');
            var key = (eq >= 0 ? segment[..eq] : segment).Trim();
            var value = eq >= 0 ? segment[(eq + 1)..].Trim() : string.Empty;
            if (!key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value.Equals("require", StringComparison.OrdinalIgnoreCase))
            {
                return SslMode.Require;
            }

            if (value.Equals("prefer", StringComparison.OrdinalIgnoreCase))
            {
                return SslMode.Prefer;
            }

            if (value.Equals("disable", StringComparison.OrdinalIgnoreCase))
            {
                return SslMode.Disable;
            }

            if (value.Equals("verify-full", StringComparison.OrdinalIgnoreCase))
            {
                return SslMode.VerifyFull;
            }

            if (value.Equals("verify-ca", StringComparison.OrdinalIgnoreCase))
            {
                return SslMode.VerifyCA;
            }

            if (Enum.TryParse<SslMode>(value, true, out var parsed))
            {
                return parsed;
            }
        }

        // Kesik URI: "...?sslmode" (değer yok) — yine de Require varsay.
        if (q.Contains("sslmode", StringComparison.OrdinalIgnoreCase))
        {
            return SslMode.Require;
        }

        return null;
    }
}
