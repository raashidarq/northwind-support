using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class PriorityRuleTests
{
    private static readonly Category General = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Outage = new("Outage", requiresSpecialist: true, forcesCriticalPriority: true);

    [Fact]
    public void Decide_UsesTheRequestedPriority() =>
        Assert.Equal(TicketPriority.High, PriorityRule.Decide(TicketPriority.High, General).Priority);

    [Fact]
    public void Decide_WithNoRequest_IsMedium() =>
        Assert.Equal(TicketPriority.Medium, PriorityRule.Decide(null, General).Priority);

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(null)]
    public void Decide_ACategoryThatForcesCritical_IsCriticalWhateverWasRequested(TicketPriority? requested)
    {
        var decision = PriorityRule.Decide(requested, Outage);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Outage", decision.Reason);
    }
}
