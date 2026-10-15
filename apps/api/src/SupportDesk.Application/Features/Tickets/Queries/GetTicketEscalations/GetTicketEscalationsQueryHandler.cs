using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Retrieves the escalation history for a ticket, ordered newest first.
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(ITicketQueries ticketQueries)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        var ticket = await ticketQueries.GetDetailAsync(ticketId, ct);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        return await ticketQueries.GetEscalationsAsync(ticketId, ct);
    }
}
