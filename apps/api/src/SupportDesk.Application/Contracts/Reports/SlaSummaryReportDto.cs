namespace SupportDesk.Application.Contracts.Reports;

/// <summary>
/// Envelope for the SLA summary report result.
/// </summary>
public sealed record SlaSummaryReportDto
{
    /// <summary>The grouping dimension ("agent" or "priority").</summary>
    public string GroupBy { get; init; } = string.Empty;

    /// <summary>Optional filter start timestamp.</summary>
    public DateTime? From { get; init; }

    /// <summary>Optional filter end timestamp.</summary>
    public DateTime? To { get; init; }

    /// <summary>The aggregated metric rows per group.</summary>
    public IReadOnlyList<SlaSummaryReportItemDto> Items { get; init; } = [];
}
