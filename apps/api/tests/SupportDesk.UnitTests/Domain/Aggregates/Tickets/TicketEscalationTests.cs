using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalationTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(5);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_RaisesThePriorityOneLevel(TicketPriority from, TicketPriority to)
    {
        var ticket = new TicketBuilder().WithPriority(from).Build();

        Escalate(ticket);

        Assert.Equal(to, ticket.Priority);
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 8)]
    [InlineData(CustomerTier.Premium, 4)]
    public void Escalate_RestartsTheSlaWindowFromNowForTheNewPriority(CustomerTier tier, int hours)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).Build();

        Escalate(ticket, tier: tier);

        Assert.Equal(Later.AddHours(hours), ticket.DueAtUtc);
    }

    [Fact]
    public void Escalate_HandsTheTicketToTheChosenAgent()
    {
        var ticket = new TicketBuilder().AssignedTo(3).Build();

        Escalate(ticket, agentId: 5);

        Assert.Equal(5, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_RecordsWhatChangedWhyWhoAndWhen()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).AssignedTo(3).Build();
        ticket.StartSlaWindow(TimeSpan.FromHours(72), FixedClock.DefaultNow);

        var escalation = Escalate(ticket, agentId: 5, reason: "  Customer is losing sales  ");

        Assert.Equal(TicketPriority.Low, escalation.FromPriority);
        Assert.Equal(TicketPriority.Medium, escalation.ToPriority);
        Assert.Equal(3, escalation.FromAgentId);
        Assert.Equal(5, escalation.ToAgentId);
        Assert.Equal(FixedClock.DefaultNow.AddHours(72), escalation.FromDueAtUtc);
        Assert.Equal(Later.AddHours(24), escalation.ToDueAtUtc);
        Assert.Equal("Customer is losing sales", escalation.Reason);
        Assert.Equal("Team lead", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
        Assert.Same(escalation, Assert.Single(ticket.Escalations));
    }

    [Fact]
    public void Escalate_ACriticalTicket_IsRejectedAndChangesNothing()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).AssignedTo(3).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket, agentId: 5));

        Assert.Contains("Critical", exception.Message);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_AResolvedOrClosedTicket_IsRejected(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Empty(ticket.Escalations);
    }

    private static TicketEscalation Escalate(
        Ticket ticket, CustomerTier tier = CustomerTier.Standard, int? agentId = null, string reason = "Customer is losing sales") =>
        ticket.Escalate(reason, "Team lead", TestSla.Policy(), tier, agentId, Later);
}
