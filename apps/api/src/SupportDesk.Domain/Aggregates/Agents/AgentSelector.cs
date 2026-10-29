namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// What the assignment rule needs to know about one agent: who they are, whether they can take
/// work, which categories they are qualified for, and how many open tickets they hold now.
/// </summary>
public sealed record AgentCandidate(
    int AgentId,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    IReadOnlyCollection<int> SpecializationCategoryIds,
    int OpenTickets);

/// <summary>The agent chosen for a ticket, or null when nobody is eligible, and why.</summary>
public sealed record AgentSelection(AgentCandidate? Agent, string Reason);

/// <summary>
/// BR-4 and BR-5: who may take a ticket, and which of them gets it.
/// </summary>
public static class AgentSelector
{
    /// <summary>
    /// Active, a specialist in the category when it requires one, and strictly below their
    /// open-ticket limit.
    /// </summary>
    public static bool IsEligible(AgentCandidate agent, int categoryId, bool requiresSpecialist) =>
        agent.IsActive
        && (!requiresSpecialist || agent.SpecializationCategoryIds.Contains(categoryId))
        && agent.OpenTickets < agent.MaxOpenTickets;

    /// <summary>
    /// The eligible agent with the fewest open tickets; ties go to the lowest agent id, so the
    /// same data always gives the same answer. Nobody eligible is a normal outcome.
    /// </summary>
    public static AgentSelection Pick(IEnumerable<AgentCandidate> agents, int categoryId, bool requiresSpecialist)
    {
        var chosen = agents
            .Where(a => IsEligible(a, categoryId, requiresSpecialist))
            .OrderBy(a => a.OpenTickets)
            .ThenBy(a => a.AgentId)
            .FirstOrDefault();

        if (chosen is null)
        {
            return new AgentSelection(null, requiresSpecialist
                ? "Left unassigned: no active specialist for this category is below their ticket limit."
                : "Left unassigned: no active agent is below their ticket limit.");
        }

        return new AgentSelection(chosen,
            $"Assigned to {chosen.FullName}, the eligible agent with the fewest open tickets ({chosen.OpenTickets}).");
    }
}
