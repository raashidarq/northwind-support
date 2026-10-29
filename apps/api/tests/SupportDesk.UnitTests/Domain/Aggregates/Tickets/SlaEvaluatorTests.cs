using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    /// <summary>25% of a one-hour window.</summary>
    private static readonly TimeSpan AtRiskPeriod = TimeSpan.FromMinutes(15);

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1), AtRiskPeriod));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now, AtRiskPeriod));

    // BR-8 boundaries: at risk from exactly the at-risk period before due, up to and including due.

    [Fact]
    public void Evaluate_JustMoreThanTheAtRiskPeriodLeft_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(Now.Add(AtRiskPeriod).AddSeconds(1), null, Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_ExactlyTheAtRiskPeriodLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now.Add(AtRiskPeriod), null, Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_ExactlyAtTheDueTime_IsAtRiskNotBreached() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Now, null, Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_OneSecondPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddSeconds(-1), null, Now, AtRiskPeriod));

    [Fact]
    public void Evaluate_ResolvedInsideTheAtRiskPeriod_IsMet() =>
        Assert.Equal(SlaStatus.Met,
            SlaEvaluator.Evaluate(Now.AddMinutes(5), Now, Now, AtRiskPeriod));
}
