namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How urgent a ticket is. The numeric values are ordered, lowest urgency first, and are
/// persisted as integers - do not renumber them.
/// </summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public static class TicketPriorityExtensions
{
    /// <summary>
    /// Returns the next priority level up, or throws if already Critical.
    /// </summary>
    public static TicketPriority Next(this TicketPriority priority) => priority switch
    {
        TicketPriority.Low => TicketPriority.Medium,
        TicketPriority.Medium => TicketPriority.High,
        TicketPriority.High => TicketPriority.Critical,
        _ => throw new InvalidOperationException($"Cannot escalate from {priority}.")
    };
}
