using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with their email and password, and issues an access token.
/// </summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger)
{
    /// <summary>The one message for every failure, so a caller cannot tell which part was wrong.</summary>
    public const string InvalidCredentials = "Email or password is incorrect.";

    /// <exception cref="UnauthorizedException">Unknown email, wrong password, or inactive agent.</exception>
    public async Task<LoginResponseDto> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        bool passwordMatches;

        if (agent?.PasswordHash is { } passwordHash)
        {
            passwordMatches = passwordHasher.Verify(passwordHash, request.Password);
        }
        else
        {
            // An unknown email still costs one slow hash, so it takes about as long as a wrong
            // password and the response time does not reveal which emails exist.
            passwordHasher.Hash(request.Password);
            passwordMatches = false;
        }

        if (agent is null || !passwordMatches || !agent.IsActive)
        {
            logger.LogInformation("Failed sign-in attempt.");
            throw new UnauthorizedException(InvalidCredentials);
        }

        var current = new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
        var (token, expiresAtUtc) = tokenIssuer.Issue(current);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponseDto(token, expiresAtUtc, current);
    }
}
