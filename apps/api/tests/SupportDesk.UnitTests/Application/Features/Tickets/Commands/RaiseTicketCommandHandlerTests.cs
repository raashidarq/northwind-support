using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    // Triage (BR-1 to BR-5): the rules are tested on their own in the domain tests; these check
    // that creating a ticket applies them and reports why.

    [Theory]
    [InlineData(CustomerTier.Standard, 8)]
    [InlineData(CustomerTier.Premium, 4)]
    public async Task HandleAsync_SetsTheDueDateFromTheSlaWindow(CustomerTier tier, int hours)
    {
        var context = new TicketCommandTestContext();
        context.ExistingCustomer(tier);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(hours), context.AddedTicket?.DueAtUtc);
        Assert.StartsWith($"{hours} h", result.Triage?.SlaWindow);
    }

    [Fact]
    public async Task HandleAsync_InACategoryThatForcesCritical_IsCritical()
    {
        var context = new TicketCommandTestContext();
        context.ExistingCategory(new Category("Outage", requiresSpecialist: false, forcesCriticalPriority: true));

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket?.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket?.DueAtUtc);
        Assert.Contains("Outage", result.Triage?.Priority);
    }

    [Fact]
    public async Task HandleAsync_AssignsTheAgentWithTheFewestOpenTickets()
    {
        var context = new TicketCommandTestContext();
        context.AgentCandidates(Candidate(4, open: 3), Candidate(6, open: 1));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(6, context.AddedTicket?.AssignedAgentId);
        Assert.Contains("Agent 6", result.Triage?.Assignment);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_StillCreatesTheTicketUnassigned()
    {
        var context = new TicketCommandTestContext();
        context.AgentCandidates(Candidate(4, open: 10, max: 10));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket?.AssignedAgentId);
        Assert.StartsWith("Left unassigned", result.Triage?.Assignment);
        context.VerifySaved(Times.Once());
    }

    private static AgentCandidate Candidate(int id, int open, int max = 10) =>
        new(id, $"Agent {id}", IsActive: true, max, [], open);
}
