using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Updates the response deadline for this ticket.
    /// </summary>
    public void SetDueAt(DateTime? dueAtUtc, DateTime nowUtc)
    {
        DueAtUtc = dueAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Applies initial automated triage decisions (priority, due date, assigned agent).
    /// </summary>
    public void ApplyTriage(TicketPriority priority, DateTime? dueAtUtc, int? agentId, DateTime nowUtc)
    {
        Priority = priority;
        DueAtUtc = dueAtUtc;
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Checks whether the ticket can be escalated. Returns true if allowed; false with a reason otherwise.
    /// </summary>
    public bool CanEscalate(out string? rejectionReason)
    {
        if (Priority == TicketPriority.Critical)
        {
            rejectionReason = "Ticket is already at Critical priority and cannot be escalated.";
            return false;
        }

        if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            rejectionReason = $"Ticket is {Status} and cannot be escalated.";
            return false;
        }

        rejectionReason = null;
        return true;
    }

    /// <summary>
    /// Escalates the ticket to a new priority level, recomputing its due date and assignee.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">Ticket is already Critical, or is Resolved/Closed.</exception>
    public void Escalate(TicketPriority newPriority, DateTime newDueAtUtc, int? newAgentId, DateTime nowUtc)
    {
        if (!CanEscalate(out var rejectionReason))
        {
            throw new BusinessRuleViolationException(rejectionReason!);
        }

        Priority = newPriority;
        DueAtUtc = newDueAtUtc;
        AssignedAgentId = newAgentId;
        UpdatedAtUtc = nowUtc;
    }
}
