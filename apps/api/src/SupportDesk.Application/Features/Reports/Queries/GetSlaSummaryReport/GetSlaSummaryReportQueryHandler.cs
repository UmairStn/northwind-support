using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummaryReport;

/// <summary>
/// Query handler that coordinates generating the SLA summary report via single database-side aggregation.
/// </summary>
public sealed class GetSlaSummaryReportQueryHandler(ISlaReportQueries queries)
{
    public Task<SlaSummaryReportDto> HandleAsync(SlaSummaryReportQuery query, CancellationToken ct) =>
        queries.GetSlaSummaryAsync(query, ct);
}
