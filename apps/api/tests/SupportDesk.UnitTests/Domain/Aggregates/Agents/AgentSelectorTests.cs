using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentSelectorTests
{
    private const int General = 1;
    private const int Billing = 2;

    [Fact]
    public void Pick_ChoosesTheEligibleAgentWithTheFewestOpenTickets()
    {
        var selection = AgentSelector.Pick([Agent(1, open: 5), Agent(2, open: 2), Agent(3, open: 4)], General, requiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.AgentId);
    }

    [Fact]
    public void Pick_OnATie_ChoosesTheLowestAgentId()
    {
        var selection = AgentSelector.Pick([Agent(7, open: 2), Agent(3, open: 2), Agent(5, open: 2)], General, requiresSpecialist: false);

        Assert.Equal(3, selection.Agent?.AgentId);
    }

    [Fact]
    public void Pick_SkipsInactiveAgents()
    {
        var selection = AgentSelector.Pick([Agent(1, open: 0, isActive: false), Agent(2, open: 3)], General, requiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.AgentId);
    }

    [Fact]
    public void Pick_SkipsAgentsAtTheirLimit()
    {
        var selection = AgentSelector.Pick([Agent(1, open: 6, max: 6), Agent(2, open: 7, max: 8)], General, requiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.AgentId);
    }

    [Fact]
    public void Pick_WhenTheCategoryNeedsASpecialist_OnlyConsidersSpecialists()
    {
        var selection = AgentSelector.Pick(
            [Agent(1, open: 0, specializations: [General]), Agent(2, open: 5, specializations: [General, Billing])],
            Billing, requiresSpecialist: true);

        Assert.Equal(2, selection.Agent?.AgentId);
    }

    [Fact]
    public void Pick_WhenTheCategoryNeedsNoSpecialist_AnyoneCanTakeIt()
    {
        var selection = AgentSelector.Pick([Agent(1, open: 0, specializations: [])], Billing, requiresSpecialist: false);

        Assert.Equal(1, selection.Agent?.AgentId);
    }

    [Fact]
    public void Pick_WhenNobodyIsEligible_LeavesItUnassignedWithAReason()
    {
        var selection = AgentSelector.Pick(
            [Agent(1, open: 6, max: 6, specializations: [Billing]), Agent(2, open: 0, specializations: [General])],
            Billing, requiresSpecialist: true);

        Assert.Null(selection.Agent);
        Assert.StartsWith("Left unassigned", selection.Reason);
    }

    private static AgentCandidate Agent(
        int id, int open, int max = 10, bool isActive = true, int[]? specializations = null) =>
        new(id, $"Agent {id}", isActive, max, specializations ?? [General], open);
}
