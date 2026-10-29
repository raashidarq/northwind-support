namespace SupportDesk.Application.Contracts.Reports;

/// <summary>
/// The SLA summary report's query string. <see cref="GroupBy"/> is a string so that a bad value
/// is a 400 from the validator rather than a model-binding error.
/// </summary>
public sealed record SlaSummaryQuery
{
    /// <summary>agent | priority (any case).</summary>
    public string? GroupBy { get; init; }

    /// <summary>Tickets created at or after this time. Optional.</summary>
    public DateTime? From { get; init; }

    /// <summary>Tickets created before this time. Optional.</summary>
    public DateTime? To { get; init; }
}

/// <summary>What the report rows are grouped by.</summary>
public enum SlaSummaryGrouping
{
    Agent,
    Priority
}

/// <summary>
/// One row of the report. The four SLA counts leave out tickets with no due date (BR-8's
/// NotApplicable), so they can add up to less than <see cref="Total"/>.
/// </summary>
/// <param name="Group">The agent's name, "Unassigned", or the priority.</param>
/// <param name="BreachRate">Breached divided by the tickets that have a due date, 0 to 1; null when none do.</param>
/// <param name="AverageResolutionHours">Created to resolved, over resolved tickets only; null when none are.</param>
public sealed record SlaSummaryRowDto(
    string Group,
    int Total,
    int Breached,
    int AtRisk,
    int Within,
    int Met,
    double? BreachRate,
    double? AverageResolutionHours);
