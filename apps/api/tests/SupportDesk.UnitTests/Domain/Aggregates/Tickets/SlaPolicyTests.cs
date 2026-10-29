using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    public void WindowFor_UsesTheBaseWindowHalvedForPremium(TicketPriority priority, CustomerTier tier, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), TestSla.Policy().WindowFor(priority, tier));

    [Fact]
    public void WindowFor_IsNeverShorterThanTheMinimum()
    {
        var policy = new SlaPolicy
        {
            BaseWindowHours = new() { [TicketPriority.Critical] = 1 },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1
        };

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_AnUnconfiguredPriority_Throws()
    {
        var policy = new SlaPolicy { MinimumWindowHours = 1 };

        Assert.Throws<InvalidOperationException>(() => policy.WindowFor(TicketPriority.High, CustomerTier.Standard));
    }

    [Fact]
    public void AtRiskPeriodFor_IsTheThresholdShareOfTheWindow() =>
        Assert.Equal(TimeSpan.FromHours(1), TestSla.Policy().AtRiskPeriodFor(TicketPriority.High, CustomerTier.Premium));
}
