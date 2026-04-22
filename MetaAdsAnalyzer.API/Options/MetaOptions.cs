namespace MetaAdsAnalyzer.API.Options;

/// <summary>
/// Sizin Meta geliştirici uygulamanız (tek AppId/AppSecret). Tüm müşteriler bu uygulama üzerinden OAuth ile
/// kendi reklam hesaplarına erişim jetonu verir; müşteri başına ayrı Meta uygulaması yoktur.
/// AppSecret üretimde Key Vault / ortam değişkeni kullanın, repoda tutmayın.
/// </summary>
public sealed class MetaOptions
{
    public const string SectionName = "Meta";

    public string AppId { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Meta geliştirici konsolunda kayıtlı tam geri dönüş URL'si (ör. https://localhost:7165/api/auth/meta/callback).</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Başarılı OAuth sonrası tarayıcı yönlendirmesi (SPA).</summary>
    public string PostLoginRedirectUri { get; set; } = "http://localhost:5173/";

    public string ApiVersion { get; set; } = "v19.0";

    /// <summary>Virgülle ayrılmış OAuth izinleri (OAuth ile otomatik istenir).</summary>
    public string Scopes { get; set; } =
        "public_profile,ads_read,ads_management,business_management";
}
