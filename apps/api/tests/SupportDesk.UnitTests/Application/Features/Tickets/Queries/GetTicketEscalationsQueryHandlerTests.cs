using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Queries;

public class GetTicketEscalationsQueryHandlerTests
{
    private readonly Mock<ITicketQueries> _ticketQueries = new();
    private readonly GetTicketEscalationsQueryHandler _handler;

    public GetTicketEscalationsQueryHandlerTests()
    {
        _handler = new GetTicketEscalationsQueryHandler(_ticketQueries.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketExists_ReturnsEscalations()
    {
        var ticketId = 10;
        _ticketQueries.Setup(q => q.GetDetailAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto { Id = ticketId });

        var expectedEscalations = new List<TicketEscalationDto>
        {
            new(1, ticketId, TicketPriority.Medium, TicketPriority.High, 1, "Agent A", 2, "Agent B",
                DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(4), "Reason", "user@example.com", DateTime.UtcNow)
        };

        _ticketQueries.Setup(q => q.GetEscalationsAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEscalations);

        var result = await _handler.HandleAsync(ticketId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(TicketPriority.High, result[0].ToPriority);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFoundException()
    {
        var ticketId = 999;
        _ticketQueries.Setup(q => q.GetDetailAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDetailDto?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync(ticketId, CancellationToken.None));

        Assert.Contains("Ticket", ex.Message);
        _ticketQueries.Verify(q => q.GetEscalationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
