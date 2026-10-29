using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of tickets. Projects straight to DTOs in the database; never tracks.
/// </summary>
public interface ITicketQueries
{
    /// <param name="query">Filters, sorting and paging, already normalized.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct);

    Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct);

    /// <summary>A customer's tickets, newest first.</summary>
    Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct);

    /// <summary>A ticket's escalation history, newest first, or null when the ticket does not exist.</summary>
    Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct);

    /// <summary>
    /// SLA counts per group for tickets created in [<paramref name="from"/>, <paramref name="to"/>);
    /// either end may be left open. Aggregated in the database, in one query.
    /// </summary>
    Task<IReadOnlyList<SlaSummaryRowDto>> GetSlaSummaryAsync(
        SlaSummaryGrouping groupBy, DateTime? from, DateTime? to, CancellationToken ct);
}
