using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Database query abstraction for SLA reporting aggregations.
/// </summary>
public interface ISlaReportQueries
{
    /// <summary>
    /// Executes a single database-side aggregation to produce the SLA summary report grouped by agent or priority.
    /// </summary>
    Task<SlaSummaryReportDto> GetSlaSummaryAsync(SlaSummaryReportQuery query, CancellationToken ct);
}
