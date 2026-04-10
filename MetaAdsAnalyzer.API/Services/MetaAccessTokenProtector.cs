using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace MetaAdsAnalyzer.API.Services;

public sealed class MetaAccessTokenProtector : IMetaAccessTokenProtector
{
    private const string Purpose = "MetaAdsAnalyzer.User.MetaAccessToken.v1";
    private readonly IDataProtector _protector;

    public MetaAccessTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        return _protector.Protect(plainText);
    }

    public bool TryUnprotect(string? stored, out string? plainText)
    {
        plainText = null;
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        try
        {
            plainText = _protector.Unprotect(stored);
            return !string.IsNullOrEmpty(plainText);
        }
        catch (CryptographicException)
        {
            plainText = stored;
            return true;
        }
    }
}
