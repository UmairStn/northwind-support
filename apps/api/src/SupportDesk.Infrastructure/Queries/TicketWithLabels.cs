using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

public sealed class TicketWithLabels
{
    public required Ticket Ticket { get; init; }

    public required Customer Customer { get; init; }

    public required Category Category { get; init; }

    public Agent? Agent { get; init; }
}
