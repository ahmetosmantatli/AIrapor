namespace MetaAdsAnalyzer.Core;

/// <summary>Meta reklam hesabı kimliğini Graph <c>act_…</c> biçimine getirir.</summary>
public static class MetaAdAccountIdNormalizer
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var t = raw.Trim();
        if (t.StartsWith("act_", StringComparison.OrdinalIgnoreCase))
        {
            return t.ToLowerInvariant();
        }

        return ("act_" + t).ToLowerInvariant();
    }
}
