using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

public sealed record PriorityDeterminationResult(TicketPriority Priority, string Reason);

/// <summary>
/// Domain service implementing BR-1: Priority calculation.
/// Category ForcesCriticalPriority takes precedence; otherwise requested priority or Medium default.
/// </summary>
public static class PriorityPolicy
{
    public static PriorityDeterminationResult DeterminePriority(
        TicketPriority? requestedPriority,
        bool forcesCriticalPriority,
        string categoryName = "Category")
    {
        if (forcesCriticalPriority)
        {
            return new PriorityDeterminationResult(
                TicketPriority.Critical,
                $"Category '{categoryName}' forces Critical priority.");
        }

        if (requestedPriority.HasValue)
        {
            return new PriorityDeterminationResult(
                requestedPriority.Value,
                $"Requested priority '{requestedPriority.Value}' applied.");
        }

        return new PriorityDeterminationResult(
            TicketPriority.Medium,
            "Defaulted to Medium as no priority was requested.");
    }
}
