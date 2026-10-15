using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Queries.Filters;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class SlaStatusTicketFilterTests
{
    private readonly Customer _customer = new("Contoso", "c@example.com", null, CustomerTier.Standard, DateTime.UtcNow);
    private readonly Category _category = new("General", false, false);
    private readonly FixedClock _clock = new();
    private readonly SlaStatusTicketFilter _filter;

    public SlaStatusTicketFilterTests()
    {
        _filter = new SlaStatusTicketFilter(_clock);
    }

    [Fact]
    public void Apply_WhenNoSlaStatusFilter_ReturnsAll()
    {
        var t1 = new TicketBuilder().WithReference("TCK-0001").Build();
        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = t1, Customer = _customer, Category = _category }
        }.AsQueryable();

        var result = _filter.Apply(tickets, new TicketQuery());

        Assert.Single(result);
    }

    [Fact]
    public void Apply_WhenNotApplicable_ReturnsTicketsWithoutDueDate()
    {
        var withDue = new TicketBuilder().WithReference("TCK-DUE").WithDueAt(_clock.UtcNow.AddHours(2)).Build();
        var withoutDue = new TicketBuilder().WithReference("TCK-NODUE").Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = withDue, Customer = _customer, Category = _category },
            new() { Ticket = withoutDue, Customer = _customer, Category = _category }
        }.AsQueryable();

        var result = _filter.Apply(tickets, new TicketQuery { SlaStatus = SlaStatus.NotApplicable }).ToList();

        var item = Assert.Single(result);
        Assert.Equal("TCK-NODUE", item.Ticket.Reference);
    }

    [Fact]
    public void Apply_WhenMet_ReturnsResolvedBeforeDueDate()
    {
        var resolvedInTime = new TicketBuilder()
            .WithReference("TCK-MET")
            .WithStatus(TicketStatus.Resolved)
            .WithDueAt(_clock.UtcNow.AddHours(4))
            .ResolvedAt(_clock.UtcNow.AddHours(2))
            .Build();

        var resolvedLate = new TicketBuilder()
            .WithReference("TCK-LATE")
            .WithStatus(TicketStatus.Resolved)
            .WithDueAt(_clock.UtcNow.AddHours(2))
            .ResolvedAt(_clock.UtcNow.AddHours(4))
            .Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = resolvedInTime, Customer = _customer, Category = _category },
            new() { Ticket = resolvedLate, Customer = _customer, Category = _category }
        }.AsQueryable();

        var result = _filter.Apply(tickets, new TicketQuery { SlaStatus = SlaStatus.Met }).ToList();

        var item = Assert.Single(result);
        Assert.Equal("TCK-MET", item.Ticket.Reference);
    }

    [Fact]
    public void Apply_WhenBreached_ReturnsResolvedLateOrOpenPastDue()
    {
        var resolvedLate = new TicketBuilder()
            .WithReference("TCK-LATE")
            .WithStatus(TicketStatus.Resolved)
            .WithDueAt(_clock.UtcNow.AddHours(-2))
            .ResolvedAt(_clock.UtcNow.AddHours(-1))
            .Build();

        var openPastDue = new TicketBuilder()
            .WithReference("TCK-OVERDUE")
            .WithStatus(TicketStatus.Open)
            .WithDueAt(_clock.UtcNow.AddHours(-1))
            .Build();

        var openInTime = new TicketBuilder()
            .WithReference("TCK-INTIME")
            .WithStatus(TicketStatus.Open)
            .WithDueAt(_clock.UtcNow.AddHours(5))
            .Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = resolvedLate, Customer = _customer, Category = _category },
            new() { Ticket = openPastDue, Customer = _customer, Category = _category },
            new() { Ticket = openInTime, Customer = _customer, Category = _category }
        }.AsQueryable();

        var result = _filter.Apply(tickets, new TicketQuery { SlaStatus = SlaStatus.Breached }).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Ticket.Reference == "TCK-LATE");
        Assert.Contains(result, x => x.Ticket.Reference == "TCK-OVERDUE");
    }

    [Fact]
    public void Apply_WhenAtRisk_ReturnsOpenTicketsInLast25PercentOfWindow()
    {
        // Total window: Created at 0h, Due at 4h.
        // At 3.5h, 0.5h remaining out of 4h = 12.5% <= 25% -> AtRisk
        var baseTime = FixedClock.DefaultNow;
        var clock = new FixedClock(baseTime.AddHours(3.5));
        var filter = new SlaStatusTicketFilter(clock);

        var atRiskTicket = new TicketBuilder()
            .WithReference("TCK-ATRISK")
            .WithStatus(TicketStatus.InProgress)
            .WithDueAt(baseTime.AddHours(4))
            .Build();

        // Safe ticket: Created at 0h, Due at 4h. But clock is at 1h -> 75% remaining -> WithinSla
        var safeTicket = new TicketBuilder()
            .WithReference("TCK-SAFE")
            .WithStatus(TicketStatus.InProgress)
            .WithDueAt(baseTime.AddHours(10))
            .Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = atRiskTicket, Customer = _customer, Category = _category },
            new() { Ticket = safeTicket, Customer = _customer, Category = _category }
        }.AsQueryable();

        var result = filter.Apply(tickets, new TicketQuery { SlaStatus = SlaStatus.AtRisk }).ToList();

        var item = Assert.Single(result);
        Assert.Equal("TCK-ATRISK", item.Ticket.Reference);
    }
}
