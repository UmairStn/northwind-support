namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public const double DefaultAtRiskThresholdPercent = 25.0;

    /// <summary>
    /// Evaluates dynamic SLA status using due date, resolution date, creation date, and the current time.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime? createdAtUtc,
        DateTime nowUtc,
        double atRiskThresholdPercent = DefaultAtRiskThresholdPercent)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (createdAtUtc.HasValue && dueAtUtc.Value > createdAtUtc.Value)
        {
            var totalWindow = dueAtUtc.Value - createdAtUtc.Value;
            var remaining = dueAtUtc.Value - nowUtc;

            // When remaining window is <= threshold percent of the total window
            if (remaining.TotalSeconds <= totalWindow.TotalSeconds * (atRiskThresholdPercent / 100.0))
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }

    /// <summary>
    /// Overload for backwards compatibility when creation time is not supplied.
    /// </summary>
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        Evaluate(dueAtUtc, resolvedAtUtc, null, nowUtc);
}
