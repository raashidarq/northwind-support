using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetEscalations;

/// <summary>
/// Returns a ticket's escalation history, newest first.
/// </summary>
public sealed class GetEscalationsQueryHandler(ITicketQueries tickets)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int id, CancellationToken ct) =>
        await tickets.GetEscalationsAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
}
