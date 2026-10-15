using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Breakdown of the automated triage decision made when a ticket is raised.
/// </summary>
public sealed record TicketTriageDto(
    TicketPriority AppliedPriority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaCalculationReason,
    int? AssignedAgentId,
    string AssignmentReason);
