using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummaryReport;

namespace SupportDesk.UnitTests.Application.Features.Reports;

public class GetSlaSummaryReportQueryHandlerTests
{
    private readonly Mock<ISlaReportQueries> _queriesMock = new();
    private readonly GetSlaSummaryReportQueryHandler _handler;

    public GetSlaSummaryReportQueryHandlerTests()
    {
        _handler = new GetSlaSummaryReportQueryHandler(_queriesMock.Object);
    }

    [Fact]
    public async Task HandleAsync_DelegatesToQueriesAndReturnsResult()
    {
        var query = new SlaSummaryReportQuery
        {
            GroupBy = "agent",
            From = DateTime.UtcNow.AddDays(-7),
            To = DateTime.UtcNow
        };

        var expectedDto = new SlaSummaryReportDto
        {
            GroupBy = "agent",
            From = query.From,
            To = query.To,
            Items =
            [
                new SlaSummaryReportItemDto
                {
                    GroupKey = "1",
                    GroupLabel = "Alex Turner",
                    AgentId = 1,
                    TotalTickets = 5,
                    BreachedCount = 1,
                    AtRiskCount = 1,
                    WithinCount = 2,
                    MetCount = 1,
                    BreachRate = 0.20,
                    AverageResolutionHours = 4.5
                }
            ]
        };

        _queriesMock
            .Setup(q => q.GetSlaSummaryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("agent", result.GroupBy);
        Assert.Single(result.Items);
        Assert.Equal("Alex Turner", result.Items[0].GroupLabel);
        Assert.Equal(0.20, result.Items[0].BreachRate);
        _queriesMock.Verify(q => q.GetSlaSummaryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }
}
