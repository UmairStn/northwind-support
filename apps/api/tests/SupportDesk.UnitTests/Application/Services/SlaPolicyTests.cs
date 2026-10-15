using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Services;

public class SlaPolicyTests
{
    private readonly SlaPolicy _policy;
    private readonly DateTime _now = FixedClock.DefaultNow;

    public SlaPolicyTests()
    {
        var options = Options.Create(new SlaOptions
        {
            BaseWindowHours = new()
            {
                ["Critical"] = 4,
                ["High"] = 8,
                ["Medium"] = 24,
                ["Low"] = 72
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1.0,
            AtRiskThresholdPercent = 25.0
        });

        _policy = new SlaPolicy(options);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void CalculateDue_ForStandardCustomer_AppliesBaseWindow(TicketPriority priority, double expectedHours)
    {
        var result = _policy.CalculateDue(_now, priority, CustomerTier.Standard);

        Assert.Equal(expectedHours, result.WindowHours);
        Assert.Equal(_now.AddHours(expectedHours), result.DueAtUtc);
        Assert.Contains("Standard", result.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void CalculateDue_ForPremiumCustomer_HalvesWindow(TicketPriority priority, double expectedHours)
    {
        var result = _policy.CalculateDue(_now, priority, CustomerTier.Premium);

        Assert.Equal(expectedHours, result.WindowHours);
        Assert.Equal(_now.AddHours(expectedHours), result.DueAtUtc);
        Assert.Contains("Premium", result.Reason);
    }

    [Fact]
    public void CalculateDue_WhenHalvedWindowIsBelowFloor_ClampsToMinimumWindowHours()
    {
        var customOptions = Options.Create(new SlaOptions
        {
            BaseWindowHours = new() { ["Critical"] = 1 },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1.0
        });

        var policy = new SlaPolicy(customOptions);

        var result = policy.CalculateDue(_now, TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(1.0, result.WindowHours);
        Assert.Equal(_now.AddHours(1), result.DueAtUtc);
        Assert.Contains("minimum floor", result.Reason);
    }
}
