using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// The agent the access token belongs to, read fresh so a deactivated agent is turned away.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(IAgentRepository agents)
{
    /// <exception cref="UnauthorizedException">The agent no longer exists or is inactive.</exception>
    public async Task<CurrentAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("This agent can no longer sign in.");
        }

        return new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
