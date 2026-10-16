using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries.Reports;

/// <summary>
/// Executes SLA summary report queries using a single database-side aggregation.
/// </summary>
public sealed class SlaReportQueries(SupportDbContext db, IClock clock) : ISlaReportQueries
{
    public async Task<SlaSummaryReportDto> GetSlaSummaryAsync(SlaSummaryReportQuery query, CancellationToken ct)
    {
        var isPriority = string.Equals(query.GroupBy?.Trim(), "priority", StringComparison.OrdinalIgnoreCase);
        var now = clock.UtcNow;

        var tickets = db.Set<Ticket>().AsNoTracking();

        if (query.From.HasValue)
        {
            tickets = tickets.Where(t => t.CreatedAtUtc >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            tickets = tickets.Where(t => t.CreatedAtUtc <= query.To.Value);
        }

        var isSqlServer = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

        IReadOnlyList<SlaSummaryReportItemDto> items = isPriority
            ? await QueryByPriorityAsync(tickets, now, isSqlServer, ct)
            : await QueryByAgentAsync(tickets, now, isSqlServer, ct);

        return new SlaSummaryReportDto
        {
            GroupBy = isPriority ? "priority" : "agent",
            From = query.From,
            To = query.To,
            Items = items
        };
    }

    private static async Task<IReadOnlyList<SlaSummaryReportItemDto>> QueryByPriorityAsync(
        IQueryable<Ticket> tickets,
        DateTime now,
        bool isSqlServer,
        CancellationToken ct)
    {
        if (isSqlServer)
        {
            var raw = await tickets
                .GroupBy(t => t.Priority)
                .Select(g => new
                {
                    Priority = g.Key,
                    TotalTickets = g.Count(),
                    MetCount = g.Count(t =>
                        t.DueAtUtc != null &&
                        (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed) &&
                        t.ResolvedAtUtc <= t.DueAtUtc),
                    BreachedCount = g.Count(t =>
                        t.DueAtUtc != null &&
                        (((t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed) && t.ResolvedAtUtc > t.DueAtUtc) ||
                         (t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && now > t.DueAtUtc))),
                    AtRiskCount = g.Count(t =>
                        t.DueAtUtc != null &&
                        t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                        now <= t.DueAtUtc &&
                        EF.Functions.DateDiffMinute(now, t.DueAtUtc.Value) * 4 <= EF.Functions.DateDiffMinute(t.CreatedAtUtc, t.DueAtUtc.Value)),
                    WithinCount = g.Count(t =>
                        t.DueAtUtc != null &&
                        t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                        now <= t.DueAtUtc &&
                        EF.Functions.DateDiffMinute(now, t.DueAtUtc.Value) * 4 > EF.Functions.DateDiffMinute(t.CreatedAtUtc, t.DueAtUtc.Value)),
                    AverageResolutionMinutes = g
                        .Where(t => t.ResolvedAtUtc != null)
                        .Average(t => (double?)EF.Functions.DateDiffMinute(t.CreatedAtUtc, t.ResolvedAtUtc!.Value))
                })
                .ToListAsync(ct);

            return raw
                .OrderByDescending(r => r.Priority)
                .Select(r => new SlaSummaryReportItemDto
                {
                    GroupKey = r.Priority.ToString(),
                    GroupLabel = r.Priority.ToString(),
                    AgentId = null,
                    Priority = r.Priority,
                    TotalTickets = r.TotalTickets,
                    BreachedCount = r.BreachedCount,
                    AtRiskCount = r.AtRiskCount,
                    WithinCount = r.WithinCount,
                    MetCount = r.MetCount,
                    BreachRate = r.TotalTickets == 0 ? 0.0 : Math.Round((double)r.BreachedCount / r.TotalTickets, 4),
                    AverageResolutionHours = r.AverageResolutionMinutes.HasValue
                        ? Math.Round(r.AverageResolutionMinutes.Value / 60.0, 2)
                        : null
                })
                .ToList();
        }

        // In-memory test double fallback
        var rawList = await tickets.ToListAsync(ct);
        return rawList
            .GroupBy(t => t.Priority)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var total = g.Count();
                var met = g.Count(t => t.DueAtUtc != null &&
                    (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed) &&
                    t.ResolvedAtUtc <= t.DueAtUtc);
                var breached = g.Count(t => t.DueAtUtc != null &&
                    (((t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed) && t.ResolvedAtUtc > t.DueAtUtc) ||
                     (t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && now > t.DueAtUtc)));
                var atRisk = g.Count(t => t.DueAtUtc != null &&
                    t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                    now <= t.DueAtUtc &&
                    (t.DueAtUtc.Value - now).TotalSeconds * 4 <= (t.DueAtUtc.Value - t.CreatedAtUtc).TotalSeconds);
                var within = g.Count(t => t.DueAtUtc != null &&
                    t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                    now <= t.DueAtUtc &&
                    (t.DueAtUtc.Value - now).TotalSeconds * 4 > (t.DueAtUtc.Value - t.CreatedAtUtc).TotalSeconds);
                var resolved = g.Where(t => t.ResolvedAtUtc != null).ToList();
                double? avgHours = resolved.Count > 0
                    ? Math.Round(resolved.Average(t => (t.ResolvedAtUtc!.Value - t.CreatedAtUtc).TotalMinutes) / 60.0, 2)
                    : null;

                return new SlaSummaryReportItemDto
                {
                    GroupKey = g.Key.ToString(),
                    GroupLabel = g.Key.ToString(),
                    AgentId = null,
                    Priority = g.Key,
                    TotalTickets = total,
                    BreachedCount = breached,
                    AtRiskCount = atRisk,
                    WithinCount = within,
                    MetCount = met,
                    BreachRate = total == 0 ? 0.0 : Math.Round((double)breached / total, 4),
                    AverageResolutionHours = avgHours
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SlaSummaryReportItemDto>> QueryByAgentAsync(
        IQueryable<Ticket> tickets,
        DateTime now,
        bool isSqlServer,
        CancellationToken ct)
    {
        var joined = from t in tickets
                     join a in db.Set<Agent>().AsNoTracking() on t.AssignedAgentId equals a.Id into agents
                     from agent in agents.DefaultIfEmpty()
                     select new { Ticket = t, Agent = agent };

        if (isSqlServer)
        {
            var raw = await joined
                .GroupBy(x => new { x.Ticket.AssignedAgentId, x.Agent.FullName })
                .Select(g => new
                {
                    AgentId = g.Key.AssignedAgentId,
                    AgentName = g.Key.FullName,
                    TotalTickets = g.Count(),
                    MetCount = g.Count(x =>
                        x.Ticket.DueAtUtc != null &&
                        (x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) &&
                        x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),
                    BreachedCount = g.Count(x =>
                        x.Ticket.DueAtUtc != null &&
                        (((x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                         (x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed && now > x.Ticket.DueAtUtc))),
                    AtRiskCount = g.Count(x =>
                        x.Ticket.DueAtUtc != null &&
                        x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                        now <= x.Ticket.DueAtUtc &&
                        EF.Functions.DateDiffMinute(now, x.Ticket.DueAtUtc.Value) * 4 <= EF.Functions.DateDiffMinute(x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc.Value)),
                    WithinCount = g.Count(x =>
                        x.Ticket.DueAtUtc != null &&
                        x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                        now <= x.Ticket.DueAtUtc &&
                        EF.Functions.DateDiffMinute(now, x.Ticket.DueAtUtc.Value) * 4 > EF.Functions.DateDiffMinute(x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc.Value)),
                    AverageResolutionMinutes = g
                        .Where(x => x.Ticket.ResolvedAtUtc != null)
                        .Average(x => (double?)EF.Functions.DateDiffMinute(x.Ticket.CreatedAtUtc, x.Ticket.ResolvedAtUtc!.Value))
                })
                .ToListAsync(ct);

            return raw
                .OrderBy(r => r.AgentName == null ? 1 : 0)
                .ThenBy(r => r.AgentName)
                .Select(r => new SlaSummaryReportItemDto
                {
                    GroupKey = r.AgentId.HasValue ? r.AgentId.Value.ToString() : "unassigned",
                    GroupLabel = r.AgentName ?? "Unassigned",
                    AgentId = r.AgentId,
                    Priority = null,
                    TotalTickets = r.TotalTickets,
                    BreachedCount = r.BreachedCount,
                    AtRiskCount = r.AtRiskCount,
                    WithinCount = r.WithinCount,
                    MetCount = r.MetCount,
                    BreachRate = r.TotalTickets == 0 ? 0.0 : Math.Round((double)r.BreachedCount / r.TotalTickets, 4),
                    AverageResolutionHours = r.AverageResolutionMinutes.HasValue
                        ? Math.Round(r.AverageResolutionMinutes.Value / 60.0, 2)
                        : null
                })
                .ToList();
        }

        // In-memory test double fallback
        var rawList = await joined.ToListAsync(ct);
        return rawList
            .GroupBy(x => new { x.Ticket.AssignedAgentId, x.Agent?.FullName })
            .OrderBy(g => g.Key.FullName == null ? 1 : 0)
            .ThenBy(g => g.Key.FullName)
            .Select(g =>
            {
                var total = g.Count();
                var met = g.Count(x => x.Ticket.DueAtUtc != null &&
                    (x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) &&
                    x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc);
                var breached = g.Count(x => x.Ticket.DueAtUtc != null &&
                    (((x.Ticket.Status == TicketStatus.Resolved || x.Ticket.Status == TicketStatus.Closed) && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                     (x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed && now > x.Ticket.DueAtUtc)));
                var atRisk = g.Count(x => x.Ticket.DueAtUtc != null &&
                    x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                    now <= x.Ticket.DueAtUtc &&
                    (x.Ticket.DueAtUtc.Value - now).TotalSeconds * 4 <= (x.Ticket.DueAtUtc.Value - x.Ticket.CreatedAtUtc).TotalSeconds);
                var within = g.Count(x => x.Ticket.DueAtUtc != null &&
                    x.Ticket.Status != TicketStatus.Resolved && x.Ticket.Status != TicketStatus.Closed &&
                    now <= x.Ticket.DueAtUtc &&
                    (x.Ticket.DueAtUtc.Value - now).TotalSeconds * 4 > (x.Ticket.DueAtUtc.Value - x.Ticket.CreatedAtUtc).TotalSeconds);
                var resolved = g.Where(x => x.Ticket.ResolvedAtUtc != null).ToList();
                double? avgHours = resolved.Count > 0
                    ? Math.Round(resolved.Average(x => (x.Ticket.ResolvedAtUtc!.Value - x.Ticket.CreatedAtUtc).TotalMinutes) / 60.0, 2)
                    : null;

                return new SlaSummaryReportItemDto
                {
                    GroupKey = g.Key.AssignedAgentId.HasValue ? g.Key.AssignedAgentId.Value.ToString() : "unassigned",
                    GroupLabel = g.Key.FullName ?? "Unassigned",
                    AgentId = g.Key.AssignedAgentId,
                    Priority = null,
                    TotalTickets = total,
                    BreachedCount = breached,
                    AtRiskCount = atRisk,
                    WithinCount = within,
                    MetCount = met,
                    BreachRate = total == 0 ? 0.0 : Math.Round((double)breached / total, 4),
                    AverageResolutionHours = avgHours
                };
            })
            .ToList();
    }
}
