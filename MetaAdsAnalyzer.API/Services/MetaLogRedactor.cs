namespace MetaAdsAnalyzer.API.Services;

/// <summary>Meta yanıtlarında access_token sızıntısını loglardan engeller.</summary>
internal static class MetaLogRedactor
{
    public static string ForLog(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        if (body.Contains("access_token", StringComparison.OrdinalIgnoreCase))
        {
            return "[gizlendi: access_token veya hassas URL içerir]";
        }

        const int max = 600;
        return body.Length <= max ? body : body[..max] + "…";
    }
}
