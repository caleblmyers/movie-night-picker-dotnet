namespace MovieNightPicker.Api.Auth;

/// <summary>
/// JWT signing/validation settings, bound from the <c>Jwt</c> configuration section.
/// The signing key is a secret — supply it via env/user-secrets, never commit a real one.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "movie-night-picker";

    public string Audience { get; set; } = "movie-night-picker";

    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeMinutes { get; set; } = 60;
}
