using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummaryReport;

namespace SupportDesk.UnitTests.Application.Features.Reports;

public class SlaSummaryReportQueryValidatorTests
{
    private readonly SlaSummaryReportQueryValidator _validator = new();

    [Theory]
    [InlineData("agent")]
    [InlineData("priority")]
    [InlineData("Agent")]
    [InlineData("PRIORITY")]
    public void Validate_WhenGroupByIsValid_Passes(string groupBy)
    {
        var query = new SlaSummaryReportQuery { GroupBy = groupBy };

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("customer")]
    [InlineData("category")]
    public void Validate_WhenGroupByIsInvalid_Fails(string? groupBy)
    {
        var query = new SlaSummaryReportQuery { GroupBy = groupBy! };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SlaSummaryReportQuery.GroupBy));
    }

    [Fact]
    public void Validate_WhenFromIsGreaterThanTo_Fails()
    {
        var query = new SlaSummaryReportQuery
        {
            GroupBy = "agent",
            From = DateTime.UtcNow.AddDays(1),
            To = DateTime.UtcNow
        };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("'From' date must be less than or equal to 'To' date"));
    }

    [Fact]
    public void Validate_WhenFromIsLessThanOrEqualToTo_Passes()
    {
        var query = new SlaSummaryReportQuery
        {
            GroupBy = "agent",
            From = DateTime.UtcNow.AddDays(-1),
            To = DateTime.UtcNow
        };

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
