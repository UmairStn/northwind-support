using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Infrastructure.Queries.Filters;

/// <summary>
/// Strategy interface for filtering ticket queries at the database level.
/// </summary>
public interface ITicketFilter
{
    IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query);
}

public sealed class StatusTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
        query.Status.HasValue
            ? source.Where(x => x.Ticket.Status == query.Status.Value)
            : source;
}

public sealed class PriorityTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
        query.Priority.HasValue
            ? source.Where(x => x.Ticket.Priority == query.Priority.Value)
            : source;
}

public sealed class CategoryTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
        query.CategoryId.HasValue
            ? source.Where(x => x.Ticket.CategoryId == query.CategoryId.Value)
            : source;
}

public sealed class CustomerTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
        query.CustomerId.HasValue
            ? source.Where(x => x.Ticket.CustomerId == query.CustomerId.Value)
            : source;
}

public sealed class AssignedAgentTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
        query.AssignedAgentId.HasValue
            ? source.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value)
            : source;
}

public sealed class UnassignedOnlyTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query) =>
        query.UnassignedOnly
            ? source.Where(x => x.Ticket.AssignedAgentId == null)
            : source;
}

public sealed class SearchTicketFilter : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return source;
        }

        var search = query.Search.Trim();
        return source.Where(x =>
            x.Ticket.Title.Contains(search) ||
            x.Ticket.Reference.Contains(search) ||
            x.Customer.Name.Contains(search));
    }
}

/// <summary>
/// Executes the composite chain of filters against a ticket query.
/// </summary>
public static class TicketFilterPipeline
{
    public static readonly IReadOnlyList<ITicketFilter> DefaultFilters =
    [
        new StatusTicketFilter(),
        new PriorityTicketFilter(),
        new CategoryTicketFilter(),
        new CustomerTicketFilter(),
        new AssignedAgentTicketFilter(),
        new UnassignedOnlyTicketFilter(),
        new SearchTicketFilter()
    ];

    public static IQueryable<TicketWithLabels> ApplyAll(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        IEnumerable<ITicketFilter>? filters = null)
    {
        var activeFilters = filters ?? DefaultFilters;
        return activeFilters.Aggregate(source, (current, filter) => filter.Apply(current, query));
    }
}
