using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Queries.Filters;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class TicketFilterTests
{
    private readonly Customer _contoso = new("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, DateTime.UtcNow);
    private readonly Customer _fabrikam = new("Fabrikam", "help@fabrikam.example", null, CustomerTier.Standard, DateTime.UtcNow);
    private readonly Category _general = new("General", false, false);
    private readonly Agent _agent = new("Alex Turner", "alex@northwind.example", 10, DateTime.UtcNow);

    [Fact]
    public void PriorityFilter_MatchesExactPriority()
    {
        var highTicket = new TicketBuilder().WithPriority(TicketPriority.High).Build();
        var lowTicket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = highTicket, Customer = _contoso, Category = _general, Agent = _agent },
            new() { Ticket = lowTicket, Customer = _contoso, Category = _general, Agent = _agent }
        }.AsQueryable();

        var filter = new PriorityTicketFilter();
        var query = new TicketQuery { Priority = TicketPriority.High };

        var result = filter.Apply(tickets, query).ToList();

        var matched = Assert.Single(result);
        Assert.Equal(TicketPriority.High, matched.Ticket.Priority);
    }

    [Fact]
    public void SearchFilter_MatchesCustomerName()
    {
        var ticket1 = new TicketBuilder().WithReference("TCK-0001").Build();
        var ticket2 = new TicketBuilder().WithReference("TCK-0002").Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = ticket1, Customer = _contoso, Category = _general, Agent = _agent },
            new() { Ticket = ticket2, Customer = _fabrikam, Category = _general, Agent = _agent }
        }.AsQueryable();

        var filter = new SearchTicketFilter();
        var query = new TicketQuery { Search = "Fabrikam" };

        var result = filter.Apply(tickets, query).ToList();

        var matched = Assert.Single(result);
        Assert.Equal("Fabrikam", matched.Customer.Name);
    }

    [Fact]
    public void Pipeline_AppliesCombinationOfFiltersWithAnd()
    {
        var matchingTicket = new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .WithStatus(TicketStatus.Open)
            .Build();

        var wrongPriority = new TicketBuilder()
            .WithPriority(TicketPriority.Low)
            .WithStatus(TicketStatus.Open)
            .Build();

        var wrongStatus = new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .WithStatus(TicketStatus.Closed)
            .Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = matchingTicket, Customer = _contoso, Category = _general },
            new() { Ticket = wrongPriority, Customer = _contoso, Category = _general },
            new() { Ticket = wrongStatus, Customer = _contoso, Category = _general }
        }.AsQueryable();

        var query = new TicketQuery
        {
            Priority = TicketPriority.Critical,
            Status = TicketStatus.Open,
            UnassignedOnly = true
        };

        var result = TicketFilterPipeline.ApplyAll(tickets, query).ToList();

        var matched = Assert.Single(result);
        Assert.Equal(TicketPriority.Critical, matched.Ticket.Priority);
        Assert.Equal(TicketStatus.Open, matched.Ticket.Status);
        Assert.Null(matched.Ticket.AssignedAgentId);
    }

    [Fact]
    public void Pipeline_SupportsAddingCustomFilterWithoutModifyingExistingCode()
    {
        // Demonstrates requirement FL-6 (Open/Closed Principle):
        // Extending the pipeline with a new filter strategy (e.g. SlaStatus in Task 2)
        var ticket1 = new TicketBuilder().WithReference("TCK-0001").Build();
        var ticket2 = new TicketBuilder().WithReference("TCK-0002").Build();

        var tickets = new List<TicketWithLabels>
        {
            new() { Ticket = ticket1, Customer = _contoso, Category = _general },
            new() { Ticket = ticket2, Customer = _contoso, Category = _general }
        }.AsQueryable();

        // Custom filter extending ITicketFilter
        var customFilter = new CustomReferenceFilter("TCK-0002");
        var customPipeline = new List<ITicketFilter>(TicketFilterPipeline.DefaultFilters) { customFilter };

        var result = TicketFilterPipeline.ApplyAll(tickets, new TicketQuery(), customPipeline).ToList();

        var matched = Assert.Single(result);
        Assert.Equal("TCK-0002", matched.Ticket.Reference);
    }

    private sealed class CustomReferenceFilter(string targetRef) : ITicketFilter
    {
        public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
            source.Where(x => x.Ticket.Reference == targetRef);
    }
}
