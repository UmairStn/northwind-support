namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Result of POST /api/tickets/{id}/escalate containing the updated ticket and the recorded escalation.
/// </summary>
public sealed record EscalateTicketResult(TicketDetailDto Ticket, TicketEscalationDto Escalation);
