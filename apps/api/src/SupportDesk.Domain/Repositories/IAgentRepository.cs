using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The agent who signs in with this email, or null.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);
}
