using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer, automatically triaging its priority, SLA window, and initial agent assignment.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agentQueries,
    ITicketTriageService triageService,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;
        var availableAgents = await agentQueries.GetAllAsync(ct);

        // Apply automated triage (BR-1 through BR-6)
        var triage = triageService.Triage(customer, category, request.RequestedPriority, availableAgents, now);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            triage.Priority,
            now);

        ticket.SetDueAt(triage.DueAtUtc, now);

        if (triage.AssignedAgentId.HasValue)
        {
            ticket.AssignTo(triage.AssignedAgentId.Value, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}. Priority: {Priority}, Agent: {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, triage.Priority, triage.AssignedAgentId);

        var detail = await getTicket.HandleAsync(ticket.Id, ct);
        detail.Triage = new TicketTriageDto(
            triage.Priority,
            triage.PriorityReason,
            triage.SlaWindowHours,
            triage.SlaCalculationReason,
            triage.AssignedAgentId,
            triage.AssignmentReason);

        return detail;
    }
}
