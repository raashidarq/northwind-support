using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Abstractions;

/// <summary>Issues the signed access token an agent sends with every request.</summary>
public interface ITokenIssuer
{
    (string Token, DateTime ExpiresAtUtc) Issue(CurrentAgentDto agent);
}
