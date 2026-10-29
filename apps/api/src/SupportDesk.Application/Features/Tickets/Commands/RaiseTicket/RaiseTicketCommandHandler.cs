using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer and triages it: priority (BR-1), due date (BR-2, BR-3) and
/// owner (BR-4, BR-5). The rules themselves live in the domain; this only gathers their inputs.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaPolicy sla,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        var priority = PriorityRule.Decide(request.RequestedPriority, category);
        var window = sla.WindowFor(priority.Priority, customer.Tier);
        var assignment = AgentSelector.Pick(
            await agents.GetCandidatesAsync(ct), category.Id, category.RequiresSpecialist);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority.Priority,
            now);

        ticket.StartSlaWindow(window, now);

        if (assignment.Agent is not null)
        {
            ticket.AssignTo(assignment.Agent.AgentId, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, due {DueAtUtc}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.DueAtUtc, ticket.AssignedAgentId);

        var created = await getTicket.HandleAsync(ticket.Id, ct);
        created.Triage = new TriageDto(priority.Reason, DescribeWindow(window, priority.Priority, customer.Tier), assignment.Reason);

        return created;
    }

    private static string DescribeWindow(TimeSpan window, TicketPriority priority, CustomerTier tier) =>
        tier == CustomerTier.Premium
            ? $"{window.TotalHours:0.##} h: the {priority} window, shortened for a Premium customer."
            : $"{window.TotalHours:0.##} h: the {priority} window.";
}
