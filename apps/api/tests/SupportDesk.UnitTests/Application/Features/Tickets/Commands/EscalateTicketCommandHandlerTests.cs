using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// The escalation rules are tested on the ticket itself (TicketEscalationTests); these check
/// the handler loads the inputs, applies the owner rule, and saves only on success.
/// </summary>
public class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest Request = new("Customer is losing sales");

    [Fact]
    public async Task HandleAsync_EscalatesSavesAndReturnsTheHistoryRow()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).Build());

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request, "Alex Turner", CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal("Customer is losing sales", result.Escalation.Reason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_KeepsTheCurrentAgentWhenStillEligible()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(3).Build());
        context.AgentCandidates(Candidate(3, open: 4), Candidate(7, open: 0));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, "Alex Turner", CancellationToken.None);

        Assert.Equal(3, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenTheCurrentAgentIsNoLongerEligible()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(3).Build());
        context.AgentCandidates(Candidate(3, open: 4, isActive: false), Candidate(7, open: 2));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, "Alex Turner", CancellationToken.None);

        Assert.Equal(7, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenTheTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(99, Request, "Alex Turner", CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ACriticalTicket_IsRejectedAndNotSaved()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, "Alex Turner", CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    private static AgentCandidate Candidate(int id, int open, bool isActive = true) =>
        new(id, $"Agent {id}", isActive, MaxOpenTickets: 10, [], open);
}
