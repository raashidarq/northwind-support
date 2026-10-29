using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of agents.
/// </summary>
public interface IAgentQueries
{
    Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct);

    /// <summary>Every agent with what the assignment rule needs, open-ticket counts included.</summary>
    Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct);
}
