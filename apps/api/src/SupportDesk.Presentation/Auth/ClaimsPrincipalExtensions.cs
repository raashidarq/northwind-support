using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SupportDesk.Presentation.Auth;

/// <summary>Reads the signed-in agent from the validated token's claims.</summary>
public static class ClaimsPrincipalExtensions
{
    public static int AgentId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("The token has no subject."));

    public static string AgentName(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Name)
            ?? throw new InvalidOperationException("The token has no name.");
}
