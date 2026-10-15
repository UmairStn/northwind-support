using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

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
/// Filters tickets in SQL Server by their dynamically evaluated SLA status before count and paging.
/// </summary>
public sealed class SlaStatusTicketFilter(IClock clock) : ITicketFilter
{
    public IQueryable<TicketWithLabels> Apply(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        if (!query.SlaStatus.HasValue)
        {
            return source;
        }

        var now = clock.UtcNow;
        var isInMemory = !source.Provider.GetType().FullName!.StartsWith("Microsoft.EntityFrameworkCore");

        if (isInMemory)
        {
            return query.SlaStatus.Value switch
            {
                SlaStatus.NotApplicable => source.Where(x => x.Ticket.DueAtUtc == null),
                SlaStatus.Met => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    (x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) &&
                    x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),
                SlaStatus.Breached => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    (((x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                     (x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed && now > x.Ticket.DueAtUtc))),
                SlaStatus.AtRisk => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                    now <= x.Ticket.DueAtUtc &&
                    (x.Ticket.DueAtUtc.Value - now).TotalSeconds * 4 <= (x.Ticket.DueAtUtc.Value - x.Ticket.CreatedAtUtc).TotalSeconds),
                SlaStatus.WithinSla => source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                    now <= x.Ticket.DueAtUtc &&
                    (x.Ticket.DueAtUtc.Value - now).TotalSeconds * 4 > (x.Ticket.DueAtUtc.Value - x.Ticket.CreatedAtUtc).TotalSeconds),
                _ => source
            };
        }

        return query.SlaStatus.Value switch
        {
            SlaStatus.NotApplicable => source.Where(x => x.Ticket.DueAtUtc == null),
            SlaStatus.Met => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                (x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) &&
                x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),
            SlaStatus.Breached => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                (((x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                 (x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed && now > x.Ticket.DueAtUtc))),
            SlaStatus.AtRisk => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                now <= x.Ticket.DueAtUtc &&
                EF.Functions.DateDiffMinute(now, x.Ticket.DueAtUtc.Value) * 4 <= EF.Functions.DateDiffMinute(x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc.Value)),
            SlaStatus.WithinSla => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                now <= x.Ticket.DueAtUtc &&
                EF.Functions.DateDiffMinute(now, x.Ticket.DueAtUtc.Value) * 4 > EF.Functions.DateDiffMinute(x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc.Value)),
            _ => source
        };
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

    public static IReadOnlyList<ITicketFilter> GetDefaultFilters(IClock clock) =>
    [
        new StatusTicketFilter(),
        new PriorityTicketFilter(),
        new CategoryTicketFilter(),
        new CustomerTicketFilter(),
        new AssignedAgentTicketFilter(),
        new UnassignedOnlyTicketFilter(),
        new SearchTicketFilter(),
        new SlaStatusTicketFilter(clock)
    ];

    public static IQueryable<TicketWithLabels> ApplyAll(
        IQueryable<TicketWithLabels> source,
        TicketQuery query) =>
        ApplyAll(source, query, DefaultFilters);

    public static IQueryable<TicketWithLabels> ApplyAll(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        IEnumerable<ITicketFilter> filters)
    {
        return filters.Aggregate(source, (current, filter) => filter.Apply(current, query));
    }
}
