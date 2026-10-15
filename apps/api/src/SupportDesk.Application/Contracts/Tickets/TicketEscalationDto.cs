using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A recorded escalation event with before/after state and the explanation given.
/// </summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    int? FromAgentId,
    string? FromAgentName,
    int? ToAgentId,
    string? ToAgentName,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);
