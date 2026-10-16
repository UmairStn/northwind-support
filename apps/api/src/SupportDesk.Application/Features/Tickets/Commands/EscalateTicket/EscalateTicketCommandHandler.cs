using FluentValidation;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;
using ApplicationValidationException = SupportDesk.Application.Exceptions.ValidationException;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket to the next priority level, recalculating its SLA due date from the current moment
/// and re-evaluating its assigned agent, while creating an immutable audit record.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    ISlaPolicy slaPolicy,
    IAgentAssignmentService assignmentService,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger,
    IValidator<EscalateTicketRequest>? validator = null)
{
    public async Task<EscalateTicketResult> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new ApplicationValidationException(validationResult.Errors
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()));
            }
        }

        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (!ticket.CanEscalate(out var rejectionReason))
        {
            throw new BusinessRuleViolationException(rejectionReason!);
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;
        var availableAgents = await agentQueries.GetAllAsync(ct);

        // Snapshot before state
        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;

        // BR-7: Raises priority by one level
        var newPriority = ticket.Priority.Next();

        // BR-7 & BR-2, BR-3: Recomputes due date starting from the exact moment of escalation
        var slaResult = slaPolicy.CalculateDue(now, newPriority, customer.Tier);

        // BR-7 & BR-5: Re-evaluate assignee (keep current agent if still eligible; otherwise reassign)
        var assignmentDecision = assignmentService.ReevaluateForEscalation(
            ticket.AssignedAgentId,
            availableAgents,
            category.Id,
            category.RequiresSpecialist,
            category.Name);

        // Apply domain escalation
        ticket.Escalate(newPriority, slaResult.DueAtUtc, assignmentDecision.AssignedAgentId, now);

        // Create immutable audit record
        var escalation = new TicketEscalation(
            ticket.Id,
            fromPriority,
            newPriority,
            fromAgentId,
            assignmentDecision.AssignedAgentId,
            fromDueAtUtc,
            slaResult.DueAtUtc,
            request.Reason,
            request.EscalatedBy ?? "Unknown",
            now);

        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}. New Due: {DueAtUtc}, Agent: {AgentId}.",
            ticket.Reference, fromPriority, newPriority, request.EscalatedBy, slaResult.DueAtUtc, assignmentDecision.AssignedAgentId);

        var updatedTicket = await getTicket.HandleAsync(ticket.Id, ct);

        var fromAgentName = availableAgents.FirstOrDefault(a => a.Id == fromAgentId)?.FullName;
        var toAgentName = assignmentDecision.AssignedAgentName
            ?? (assignmentDecision.AssignedAgentId.HasValue
                ? availableAgents.FirstOrDefault(a => a.Id == assignmentDecision.AssignedAgentId.Value)?.FullName
                : null);

        var escalationDto = new TicketEscalationDto(
            escalation.Id,
            escalation.TicketId,
            escalation.FromPriority,
            escalation.ToPriority,
            escalation.FromAgentId,
            fromAgentName,
            escalation.ToAgentId,
            toAgentName,
            escalation.FromDueAtUtc,
            escalation.ToDueAtUtc,
            escalation.Reason,
            escalation.EscalatedBy,
            escalation.EscalatedAtUtc);

        return new EscalateTicketResult(updatedTicket, escalationDto);
    }
}
