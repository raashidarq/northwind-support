using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>The priority a new ticket gets, and why.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);

/// <summary>
/// BR-1: the requested priority, Medium when none was requested, and always Critical for a
/// category flagged <see cref="Category.ForcesCriticalPriority"/>.
/// </summary>
public static class PriorityRule
{
    public static PriorityDecision Decide(TicketPriority? requested, Category category)
    {
        if (category.ForcesCriticalPriority)
        {
            return new PriorityDecision(TicketPriority.Critical, $"{category.Name} tickets are always Critical.");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"{priority}, as requested.")
            : new PriorityDecision(TicketPriority.Medium, "No priority was requested, so Medium.");
    }
}
