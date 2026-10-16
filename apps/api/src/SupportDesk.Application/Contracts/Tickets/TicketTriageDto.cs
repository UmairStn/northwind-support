using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Breakdown of the automated triage decision made when a ticket is raised (PDF 2.3 A).
/// </summary>
public sealed record TicketTriageDto(
    TicketPriority AppliedPriority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    int? AssignedAgentId,
    string AssignmentReason)
{
    public TicketPriority Priority => AppliedPriority;
    public string SlaCalculationReason => SlaReason;
}
