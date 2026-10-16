namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Body of POST /api/tickets/{id}/escalate. EscalatedBy is derived from the auth token (AU-8).
/// </summary>
public sealed record EscalateTicketRequest(string Reason, string? EscalatedBy = null);
