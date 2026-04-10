using System.ComponentModel.DataAnnotations;

namespace MetaAdsAnalyzer.API.Models;

public sealed class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    public string Password { get; set; } = null!;
}

public sealed class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}

public sealed class AuthResponseDto
{
    public string AccessToken { get; set; } = null!;

    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
