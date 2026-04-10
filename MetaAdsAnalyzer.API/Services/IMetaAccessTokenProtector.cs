namespace MetaAdsAnalyzer.API.Services;

/// <summary>
/// Kullanıcı Meta erişim jetonunu veritabanında ASP.NET Data Protection ile saklar.
/// Çok sunucuda üretimde ortak anahtar deposu (ör. Azure Blob) yapılandırılmalıdır.
/// </summary>
public interface IMetaAccessTokenProtector
{
    string Protect(string plainText);

    /// <summary>Çözüm başarısızsa (eski düz metin kayıtlar) düz metin olarak döner.</summary>
    bool TryUnprotect(string? stored, out string? plainText);
}
