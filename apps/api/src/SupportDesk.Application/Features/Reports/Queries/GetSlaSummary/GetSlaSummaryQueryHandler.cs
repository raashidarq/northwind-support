using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

/// <summary>
/// SLA counts per agent or per priority for tickets created in a date range (2.5). The
/// query has already passed <see cref="SlaSummaryQueryValidator"/>.
/// </summary>
public sealed class GetSlaSummaryQueryHandler(ITicketQueries tickets)
{
    public Task<IReadOnlyList<SlaSummaryRowDto>> HandleAsync(SlaSummaryQuery query, CancellationToken ct) =>
        tickets.GetSlaSummaryAsync(
            Enum.Parse<SlaSummaryGrouping>(query.GroupBy!, ignoreCase: true),
            AsUtc(query.From),
            AsUtc(query.To),
            ct);

    /// <summary>A date with no "Z" or offset is taken to be UTC, like every timestamp we store.</summary>
    private static DateTime? AsUtc(DateTime? value) =>
        value is { Kind: DateTimeKind.Unspecified } unspecified
            ? DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)
            : value?.ToUniversalTime();
}
