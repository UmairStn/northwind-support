using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummaryReport;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    /// <summary>
    /// Returns the SLA summary report aggregated by agent or priority.
    /// </summary>
    [HttpGet("sla-summary")]
    public Task<SlaSummaryReportDto> GetSlaSummary(
        [FromQuery] SlaSummaryReportQuery query,
        [FromServices] GetSlaSummaryReportQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);
}
