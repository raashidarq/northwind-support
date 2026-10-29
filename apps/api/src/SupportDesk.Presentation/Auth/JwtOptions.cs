namespace SupportDesk.Presentation.Auth;

/// <summary>The <c>Jwt</c> configuration section. The key is never in code; see .env.example.</summary>
public sealed class JwtOptions
{
    /// <summary>The HMAC signing key: at least 32 characters, kept secret.</summary>
    public string Key { get; init; } = string.Empty;

    public string Issuer { get; init; } = "northwind-support";

    public string Audience { get; init; } = "northwind-support";

    public int LifetimeMinutes { get; init; } = 60;
}
