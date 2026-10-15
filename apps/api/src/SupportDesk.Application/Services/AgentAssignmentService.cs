using SupportDesk.Application.Contracts.Agents;

namespace SupportDesk.Application.Services;

public sealed record AssignmentDecision(
    int? AssignedAgentId,
    string? AssignedAgentName,
    string Reason);

public interface IAgentAssignmentService
{
    AssignmentDecision Assign(
        IEnumerable<AgentDto> candidates,
        int categoryId,
        bool requiresSpecialist,
        string categoryName = "Category");

    AssignmentDecision ReevaluateForEscalation(
        int? currentAgentId,
        IEnumerable<AgentDto> candidates,
        int categoryId,
        bool requiresSpecialist,
        string categoryName = "Category");
}

/// <summary>
/// Domain service implementing BR-4 and BR-5: Deterministic agent selection based on
/// activity, specialization, and lowest open ticket count with deterministic tie-breaking.
/// </summary>
public sealed class AgentAssignmentService : IAgentAssignmentService
{
    public AssignmentDecision Assign(
        IEnumerable<AgentDto> candidates,
        int categoryId,
        bool requiresSpecialist,
        string categoryName = "Category")
    {
        var candidateList = candidates.ToList();

        var eligible = candidateList
            .Where(a => a.IsActive)
            .Where(a => !requiresSpecialist || a.Specializations.Any(s => s.Id == categoryId))
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .ToList();

        if (eligible.Count > 0)
        {
            var chosen = eligible[0];
            return new AssignmentDecision(
                chosen.Id,
                chosen.FullName,
                $"Assigned to {chosen.FullName} with {chosen.OpenTicketCount} open tickets.");
        }

        // Generate specific explanatory reason
        string unassignedReason;
        if (requiresSpecialist)
        {
            var activeSpecialists = candidateList
                .Where(a => a.IsActive && a.Specializations.Any(s => s.Id == categoryId))
                .ToList();

            if (activeSpecialists.Count == 0)
            {
                unassignedReason = $"Unassigned: No active specialists found for category '{categoryName}'.";
            }
            else
            {
                unassignedReason = $"Unassigned: All active specialists for category '{categoryName}' are at their maximum ticket capacity.";
            }
        }
        else
        {
            var activeAgents = candidateList.Where(a => a.IsActive).ToList();
            if (activeAgents.Count == 0)
            {
                unassignedReason = "Unassigned: No active support agents available.";
            }
            else
            {
                unassignedReason = "Unassigned: All active agents are at their maximum ticket capacity.";
            }
        }

        return new AssignmentDecision(null, null, unassignedReason);
    }

    public AssignmentDecision ReevaluateForEscalation(
        int? currentAgentId,
        IEnumerable<AgentDto> candidates,
        int categoryId,
        bool requiresSpecialist,
        string categoryName = "Category")
    {
        var candidateList = candidates.ToList();

        if (currentAgentId.HasValue)
        {
            var currentAgent = candidateList.FirstOrDefault(a => a.Id == currentAgentId.Value);
            if (currentAgent is not null && IsEligibleForRetention(currentAgent, categoryId, requiresSpecialist))
            {
                return new AssignmentDecision(
                    currentAgent.Id,
                    currentAgent.FullName,
                    $"Retained current agent {currentAgent.FullName}.");
            }
        }

        // If current agent is not eligible or ticket was unassigned, perform standard assignment
        return Assign(candidateList, categoryId, requiresSpecialist, categoryName);
    }

    private static bool IsEligibleForRetention(AgentDto agent, int categoryId, bool requiresSpecialist)
    {
        if (!agent.IsActive)
        {
            return false;
        }

        if (requiresSpecialist && !agent.Specializations.Any(s => s.Id == categoryId))
        {
            return false;
        }

        // Since the ticket is already held by this agent, open ticket count is within limit
        if (agent.OpenTicketCount > agent.MaxOpenTickets)
        {
            return false;
        }

        return true;
    }
}
