using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket (BR-7). The rules live in <see cref="Ticket.Escalate"/> and
/// <see cref="AgentSelector.KeepOrPick"/>; this only gathers their inputs.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    ITicketQueries ticketQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaPolicy sla,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="BusinessRuleViolationException">The ticket is Critical, Resolved or Closed.</exception>
    public async Task<EscalationResultDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // A ticket always has both; they cannot be deleted while it refers to them.
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var owner = AgentSelector.KeepOrPick(
            await agents.GetCandidatesAsync(ct), ticket.AssignedAgentId, category.Id, category.RequiresSpecialist);

        var escalation = ticket.Escalate(
            request.Reason, request.EscalatedBy, sla, customer.Tier, owner.Agent?.AgentId, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}. {Owner}",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy, owner.Reason);

        var history = await ticketQueries.GetEscalationsAsync(id, ct) ?? [];

        return new EscalationResultDto(
            await getTicket.HandleAsync(id, ct),
            history.First(e => e.Id == escalation.Id));
    }
}
