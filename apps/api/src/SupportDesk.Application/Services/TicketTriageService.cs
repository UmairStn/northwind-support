using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    DateTime DueAtUtc,
    double SlaWindowHours,
    string SlaCalculationReason,
    int? AssignedAgentId,
    string? AssignedAgentName,
    string AssignmentReason);

public interface ITicketTriageService
{
    TriageDecision Triage(
        Customer customer,
        Category category,
        TicketPriority? requestedPriority,
        IReadOnlyList<AgentDto> agents,
        DateTime nowUtc);
}

/// <summary>
/// Domain / application orchestrator combining priority calculation, SLA calculation, and agent assignment.
/// </summary>
public sealed class TicketTriageService(
    ISlaPolicy slaPolicy,
    IAgentAssignmentService assignmentService) : ITicketTriageService
{
    public TriageDecision Triage(
        Customer customer,
        Category category,
        TicketPriority? requestedPriority,
        IReadOnlyList<AgentDto> agents,
        DateTime nowUtc)
    {
        // BR-1: Priority determination
        var priorityResult = PriorityPolicy.DeterminePriority(
            requestedPriority,
            category.ForcesCriticalPriority,
            category.Name);

        // BR-2 & BR-3: SLA calculation
        var slaResult = slaPolicy.CalculateDue(nowUtc, priorityResult.Priority, customer.Tier);

        // BR-4 & BR-5: Agent assignment
        var assignmentResult = assignmentService.Assign(
            agents,
            category.Id,
            category.RequiresSpecialist,
            category.Name);

        return new TriageDecision(
            priorityResult.Priority,
            priorityResult.Reason,
            slaResult.DueAtUtc,
            slaResult.WindowHours,
            slaResult.Reason,
            assignmentResult.AssignedAgentId,
            assignmentResult.AssignedAgentName,
            assignmentResult.Reason);
    }
}
