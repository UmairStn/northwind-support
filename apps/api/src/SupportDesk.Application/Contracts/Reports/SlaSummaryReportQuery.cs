namespace SupportDesk.Application.Contracts.Reports;

/// <summary>
/// Query parameters for the SLA summary report.
/// </summary>
public sealed record SlaSummaryReportQuery
{
    /// <summary>Filter tickets created on or after this UTC timestamp.</summary>
    public DateTime? From { get; init; }

    /// <summary>Filter tickets created on or before this UTC timestamp.</summary>
    public DateTime? To { get; init; }

    /// <summary>Dimension to group by: "agent" or "priority".</summary>
    public string GroupBy { get; init; } = string.Empty;
}
