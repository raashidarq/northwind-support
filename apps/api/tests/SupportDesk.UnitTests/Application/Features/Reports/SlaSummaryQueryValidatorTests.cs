using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

namespace SupportDesk.UnitTests.Application.Features.Reports;

public class SlaSummaryQueryValidatorTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    private readonly SlaSummaryQueryValidator _validator = new();

    [Theory]
    [InlineData("agent", true)]
    [InlineData("PRIORITY", true)]
    [InlineData("team", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void GroupBy_MustBeAgentOrPriority(string? groupBy, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new SlaSummaryQuery { GroupBy = groupBy }).IsValid);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void To_MustBeLaterThanFrom(int toOffsetHours, bool valid) =>
        Assert.Equal(valid, _validator.Validate(
            new SlaSummaryQuery { GroupBy = "agent", From = Now, To = Now.AddHours(toOffsetHours) }).IsValid);

    [Fact]
    public void DateRange_IsOptional() =>
        Assert.True(_validator.Validate(new SlaSummaryQuery { GroupBy = "agent", To = Now }).IsValid);
}
