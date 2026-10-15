using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Services;

public class PriorityPolicyTests
{
    [Fact]
    public void DeterminePriority_WhenCategoryForcesCritical_ReturnsCriticalRegardlessOfRequested()
    {
        var result = PriorityPolicy.DeterminePriority(TicketPriority.Low, forcesCriticalPriority: true, "Security");

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Contains("Security", result.Reason);
        Assert.Contains("forces Critical", result.Reason);
    }

    [Fact]
    public void DeterminePriority_WithRequestedPriority_AppliesRequestedPriority()
    {
        var result = PriorityPolicy.DeterminePriority(TicketPriority.High, forcesCriticalPriority: false, "General");

        Assert.Equal(TicketPriority.High, result.Priority);
        Assert.Contains("High", result.Reason);
    }

    [Fact]
    public void DeterminePriority_WithoutRequestedPriority_DefaultsToMedium()
    {
        var result = PriorityPolicy.DeterminePriority(null, forcesCriticalPriority: false, "General");

        Assert.Equal(TicketPriority.Medium, result.Priority);
        Assert.Contains("Medium", result.Reason);
    }
}
