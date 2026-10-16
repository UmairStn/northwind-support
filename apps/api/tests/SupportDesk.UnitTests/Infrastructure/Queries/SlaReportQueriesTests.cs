using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries.Reports;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class SlaReportQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly FixedClock _clock = new();
    private readonly SlaReportQueries _queries;

    public SlaReportQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        _queries = new SlaReportQueries(_db, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetSlaSummaryAsync_WhenGroupByPriority_AggregatesSlaStatusesCorrectly()
    {
        // Arrange
        var customer = new Customer("Contoso", "contoso@example.com", null, CustomerTier.Standard, _clock.UtcNow);
        var category = new Category("General", false, false);
        _db.AddRange(customer, category);
        await _db.SaveChangesAsync();

        var baseTime = FixedClock.DefaultNow;

        // 1. Critical Met ticket: resolved 1h before due
        var criticalMet = new TicketBuilder()
            .WithReference("TCK-CRIT-MET")
            .WithPriority(TicketPriority.Critical)
            .WithStatus(TicketStatus.Resolved)
            .WithDueAt(baseTime.AddHours(2))
            .ResolvedAt(baseTime.AddHours(1))
            .Build();

        // 2. Critical Breached ticket: resolved 1h late
        var criticalBreached = new TicketBuilder()
            .WithReference("TCK-CRIT-BREACHED")
            .WithPriority(TicketPriority.Critical)
            .WithStatus(TicketStatus.Resolved)
            .WithDueAt(baseTime.AddHours(2))
            .ResolvedAt(baseTime.AddHours(3))
            .Build();

        // 3. High At-Risk ticket: due at 4h. At clock=3.5h, 0.5h remaining out of 4h window = 12.5% <= 25%
        var highAtRisk = new TicketBuilder()
            .WithReference("TCK-HIGH-ATRISK")
            .WithPriority(TicketPriority.High)
            .WithStatus(TicketStatus.Open)
            .WithDueAt(baseTime.AddHours(4))
            .Build();

        // 4. High Within-SLA ticket: due at 10h. At clock=3.5h, 6.5h remaining out of 10h window = 65% > 25%
        var highWithin = new TicketBuilder()
            .WithReference("TCK-HIGH-WITHIN")
            .WithPriority(TicketPriority.High)
            .WithStatus(TicketStatus.Open)
            .WithDueAt(baseTime.AddHours(10))
            .Build();

        _db.AddRange(criticalMet, criticalBreached, highAtRisk, highWithin);
        await _db.SaveChangesAsync();

        // Advance clock to 3.5h so highAtRisk enters the <= 25% window
        _clock.Advance(TimeSpan.FromHours(3.5));

        // Act
        var result = await _queries.GetSlaSummaryAsync(
            new SlaSummaryReportQuery { GroupBy = "priority" },
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("priority", result.GroupBy);

        var criticalGroup = Assert.Single(result.Items, i => i.Priority == TicketPriority.Critical);
        Assert.Equal(2, criticalGroup.TotalTickets);
        Assert.Equal(1, criticalGroup.MetCount);
        Assert.Equal(1, criticalGroup.BreachedCount);
        Assert.Equal(0, criticalGroup.AtRiskCount);
        Assert.Equal(0, criticalGroup.WithinCount);
        Assert.Equal(0.50, criticalGroup.BreachRate);
        Assert.NotNull(criticalGroup.AverageResolutionHours);

        var highGroup = Assert.Single(result.Items, i => i.Priority == TicketPriority.High);
        Assert.Equal(2, highGroup.TotalTickets);
        Assert.Equal(0, highGroup.MetCount);
        Assert.Equal(0, highGroup.BreachedCount);
        Assert.Equal(1, highGroup.AtRiskCount);
        Assert.Equal(1, highGroup.WithinCount);
        Assert.Equal(0.0, highGroup.BreachRate);
        Assert.Null(highGroup.AverageResolutionHours); // No resolved tickets in High
    }

    [Fact]
    public async Task GetSlaSummaryAsync_WhenGroupByAgent_IncludesUnassignedAndAssignedAgents()
    {
        // Arrange
        var agent = new Agent("Alex Turner", "alex@example.com", 10, _clock.UtcNow);
        var customer = new Customer("Fabrikam", "fab@example.com", null, CustomerTier.Standard, _clock.UtcNow);
        var category = new Category("Technical", false, false);
        _db.AddRange(agent, customer, category);
        await _db.SaveChangesAsync();

        var assignedTicket = new TicketBuilder()
            .WithReference("TCK-ASSIGNED")
            .AssignedTo(agent.Id)
            .WithDueAt(_clock.UtcNow.AddHours(4))
            .Build();

        var unassignedTicket = new TicketBuilder()
            .WithReference("TCK-UNASSIGNED")
            .AssignedTo(null)
            .WithDueAt(_clock.UtcNow.AddHours(-2)) // Overdue / breached
            .Build();

        _db.AddRange(assignedTicket, unassignedTicket);
        await _db.SaveChangesAsync();

        // Act
        var result = await _queries.GetSlaSummaryAsync(
            new SlaSummaryReportQuery { GroupBy = "agent" },
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("agent", result.GroupBy);
        Assert.Equal(2, result.Items.Count);

        var agentItem = Assert.Single(result.Items, i => i.AgentId == agent.Id);
        Assert.Equal("Alex Turner", agentItem.GroupLabel);
        Assert.Equal(1, agentItem.TotalTickets);
        Assert.Equal(0, agentItem.BreachedCount);

        var unassignedItem = Assert.Single(result.Items, i => i.AgentId == null);
        Assert.Equal("Unassigned", unassignedItem.GroupLabel);
        Assert.Equal("unassigned", unassignedItem.GroupKey);
        Assert.Equal(1, unassignedItem.TotalTickets);
        Assert.Equal(1, unassignedItem.BreachedCount);
        Assert.Equal(1.0, unassignedItem.BreachRate);
    }

    [Fact]
    public async Task GetSlaSummaryAsync_WhenDateRangeApplied_FiltersCorrectly()
    {
        // Arrange
        var customer = new Customer("Wide World", "ww@example.com", null, CustomerTier.Standard, _clock.UtcNow);
        var category = new Category("Billing", false, false);
        _db.AddRange(customer, category);
        await _db.SaveChangesAsync();

        var ticketInRange = new TicketBuilder()
            .WithReference("TCK-IN")
            .Build();

        _db.Add(ticketInRange);
        await _db.SaveChangesAsync();

        // Act: Filter window strictly after creation time
        var resultOutOfRange = await _queries.GetSlaSummaryAsync(
            new SlaSummaryReportQuery
            {
                GroupBy = "priority",
                From = _clock.UtcNow.AddDays(1),
                To = _clock.UtcNow.AddDays(2)
            },
            CancellationToken.None);

        // Assert: Out-of-range yields 0 groups
        Assert.Empty(resultOutOfRange.Items);

        // Act: Filter window that covers creation time
        var resultInRange = await _queries.GetSlaSummaryAsync(
            new SlaSummaryReportQuery
            {
                GroupBy = "priority",
                From = _clock.UtcNow.AddDays(-1),
                To = _clock.UtcNow.AddDays(1)
            },
            CancellationToken.None);

        Assert.Single(resultInRange.Items);
        Assert.Equal(1, resultInRange.Items[0].TotalTickets);
    }
}
