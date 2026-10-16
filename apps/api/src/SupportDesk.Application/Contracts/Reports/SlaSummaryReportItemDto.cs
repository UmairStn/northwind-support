using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Reports;

/// <summary>
/// SLA metrics aggregated for one group (either an agent or a priority level).
/// </summary>
public sealed record SlaSummaryReportItemDto
{
    /// <summary>Unique identifier for the group (e.g. "1", "unassigned", or "Critical").</summary>
    public string GroupKey { get; init; } = string.Empty;

    /// <summary>Human-readable display name for the group (e.g. "Alex Turner", "Unassigned", "Critical").</summary>
    public string GroupLabel { get; init; } = string.Empty;

    /// <summary>Assigned agent ID when grouped by agent; null for unassigned or when grouped by priority.</summary>
    public int? AgentId { get; init; }

    /// <summary>Ticket priority when grouped by priority; null when grouped by agent.</summary>
    public TicketPriority? Priority { get; init; }

    /// <summary>Total tickets in this group within the date range.</summary>
    public int TotalTickets { get; init; }

    /// <summary>Count of tickets that have breached their SLA.</summary>
    public int BreachedCount { get; init; }

    /// <summary>Count of open tickets at risk of breaching their SLA.</summary>
    public int AtRiskCount { get; init; }

    /// <summary>Count of open tickets comfortably within their SLA.</summary>
    public int WithinCount { get; init; }

    /// <summary>Count of tickets resolved or closed on or before their due date.</summary>
    public int MetCount { get; init; }

    /// <summary>Ratio of breached tickets to total tickets (0.0 to 1.0, 4 decimal places).</summary>
    public double BreachRate { get; init; }

    /// <summary>Average hours from creation to resolution for resolved tickets; null if no resolved tickets.</summary>
    public double? AverageResolutionHours { get; init; }
}
