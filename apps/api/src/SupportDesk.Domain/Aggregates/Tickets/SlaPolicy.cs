using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The SLA numbers (BR-2, BR-3, BR-8). Bound from the <c>Sla</c> configuration section, so they
/// can change without touching code (BR-6).
/// </summary>
public sealed class SlaPolicy
{
    /// <summary>Hours a ticket of each priority has before it is due.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; init; } = [];

    /// <summary>Applied to the base window for Premium customers, e.g. 0.5 for half.</summary>
    public double PremiumCustomerMultiplier { get; init; }

    /// <summary>No window is ever shorter than this.</summary>
    public double MinimumWindowHours { get; init; }

    /// <summary>An open ticket is at risk once this percentage of its window, or less, remains.</summary>
    public double AtRiskThresholdPercent { get; init; }

    /// <summary>How long a ticket has from the start of its window until it is due.</summary>
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        if (!BaseWindowHours.TryGetValue(priority, out var hours))
        {
            throw new InvalidOperationException($"No SLA window is configured for {priority} priority.");
        }

        if (tier == CustomerTier.Premium)
        {
            hours *= PremiumCustomerMultiplier;
        }

        return TimeSpan.FromHours(Math.Max(hours, MinimumWindowHours));
    }

    /// <summary>The last stretch of the window, during which an open ticket counts as at risk.</summary>
    public TimeSpan AtRiskPeriodFor(TicketPriority priority, CustomerTier tier) =>
        WindowFor(priority, tier) * (AtRiskThresholdPercent / 100);
}
