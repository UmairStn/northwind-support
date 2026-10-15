using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Services;

namespace SupportDesk.UnitTests.Application.Services;

public class AgentAssignmentServiceTests
{
    private readonly AgentAssignmentService _service = new();

    private static AgentDto MakeAgent(
        int id,
        string name,
        bool isActive,
        int maxOpen,
        int openCount,
        params int[] specializations) =>
        new(
            id,
            name,
            $"{name.ToLower().Replace(" ", ".")}@example.com",
            isActive,
            maxOpen,
            openCount,
            specializations.Select(s => new CategorySummaryDto(s, $"Category {s}")).ToList());

    [Fact]
    public void Assign_PicksEligibleAgentWithFewestOpenTickets()
    {
        var agents = new[]
        {
            MakeAgent(1, "Alex", true, 10, 5, 1),
            MakeAgent(2, "Ben", true, 8, 3, 1),
            MakeAgent(3, "Sara", true, 6, 4, 1),
        };

        var result = _service.Assign(agents, categoryId: 1, requiresSpecialist: false);

        Assert.Equal(2, result.AssignedAgentId);
        Assert.Equal("Ben", result.AssignedAgentName);
        Assert.Contains("3 open tickets", result.Reason);
    }

    [Fact]
    public void Assign_BreaksTiesDeterministicallyWithLowestAgentId()
    {
        var agents = new[]
        {
            MakeAgent(4, "Sara", true, 6, 2, 1),
            MakeAgent(2, "Ben", true, 8, 2, 1),
            MakeAgent(1, "Alex", true, 10, 2, 1),
        };

        var result = _service.Assign(agents, categoryId: 1, requiresSpecialist: false);

        // Lowest ID is 1
        Assert.Equal(1, result.AssignedAgentId);
    }

    [Fact]
    public void Assign_ExcludesInactiveAgents()
    {
        var agents = new[]
        {
            MakeAgent(6, "Yuki", false, 8, 0, 1),
            MakeAgent(1, "Alex", true, 10, 4, 1),
        };

        var result = _service.Assign(agents, categoryId: 1, requiresSpecialist: false);

        Assert.Equal(1, result.AssignedAgentId);
    }

    [Fact]
    public void Assign_ExcludesAgentsAtOrAboveCapacity()
    {
        var agents = new[]
        {
            MakeAgent(5, "Marcus", true, 6, 6, 1), // At capacity
            MakeAgent(1, "Alex", true, 10, 5, 1),
        };

        var result = _service.Assign(agents, categoryId: 1, requiresSpecialist: false);

        Assert.Equal(1, result.AssignedAgentId);
    }

    [Fact]
    public void Assign_WhenCategoryRequiresSpecialist_OnlySelectsSpecializedAgents()
    {
        // Category 2 (Billing): Only Sara (4) is specialized in Category 2
        var agents = new[]
        {
            MakeAgent(1, "Alex", true, 10, 1, 1), // 1 ticket, but not specialized in Cat 2
            MakeAgent(4, "Sara", true, 6, 4, 2),  // 4 tickets, specialized in Cat 2
        };

        var result = _service.Assign(agents, categoryId: 2, requiresSpecialist: true, "Billing");

        Assert.Equal(4, result.AssignedAgentId);
        Assert.Equal("Sara", result.AssignedAgentName);
    }

    [Fact]
    public void Assign_WhenNobodyIsEligible_ReturnsUnassignedWithExplanatoryReason()
    {
        var agents = new[]
        {
            MakeAgent(5, "Marcus", true, 6, 6, 2), // Specialist, but at capacity
        };

        var result = _service.Assign(agents, categoryId: 2, requiresSpecialist: true, "Billing");

        Assert.Null(result.AssignedAgentId);
        Assert.Contains("Unassigned", result.Reason);
        Assert.Contains("maximum ticket capacity", result.Reason);
    }

    [Fact]
    public void ReevaluateForEscalation_RetainsCurrentAgentIfStillEligible()
    {
        var agents = new[]
        {
            MakeAgent(1, "Alex", true, 10, 5, 1),
            MakeAgent(2, "Ben", true, 8, 2, 1), // Fewer tickets
        };

        // Current agent is Alex (1) and Alex is still eligible
        var result = _service.ReevaluateForEscalation(1, agents, categoryId: 1, requiresSpecialist: false);

        Assert.Equal(1, result.AssignedAgentId);
        Assert.Contains("Retained current agent", result.Reason);
    }

    [Fact]
    public void ReevaluateForEscalation_ReassignsIfCurrentAgentIsNoLongerEligible()
    {
        var agents = new[]
        {
            MakeAgent(1, "Alex", false, 10, 5, 1), // Alex became inactive
            MakeAgent(2, "Ben", true, 8, 2, 1),
        };

        var result = _service.ReevaluateForEscalation(1, agents, categoryId: 1, requiresSpecialist: false);

        Assert.Equal(2, result.AssignedAgentId);
        Assert.Equal("Ben", result.AssignedAgentName);
    }
}
