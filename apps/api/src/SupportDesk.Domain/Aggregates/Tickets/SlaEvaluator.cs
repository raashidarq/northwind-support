namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <param name="atRiskPeriod">
    /// The last stretch before the due date in which an open ticket is at risk
    /// (<see cref="SlaPolicy.AtRiskPeriodFor"/>).
    /// </param>
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc, TimeSpan atRiskPeriod) =>
        dueAtUtc is null ? SlaStatus.NotApplicable
        : resolvedAtUtc is not null
            ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
        : nowUtc > dueAtUtc ? SlaStatus.Breached
        : dueAtUtc - nowUtc <= atRiskPeriod ? SlaStatus.AtRisk
        : SlaStatus.WithinSla;
}
