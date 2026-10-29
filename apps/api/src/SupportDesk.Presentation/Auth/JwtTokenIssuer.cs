using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Presentation.Auth;

/// <summary>Issues short-lived JWTs signed with the configured key (HMAC-SHA256).</summary>
public sealed class JwtTokenIssuer(JwtOptions options, IClock clock) : ITokenIssuer
{
    public (string Token, DateTime ExpiresAtUtc) Issue(CurrentAgentDto agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.LifetimeMinutes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
                new Claim(JwtRegisteredClaimNames.Email, agent.Email)
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(options), SecurityAlgorithms.HmacSha256)
        });

        return (token, expiresAtUtc);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.Key));
}
