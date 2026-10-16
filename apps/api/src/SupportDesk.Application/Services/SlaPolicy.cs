using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

public sealed record SlaCalculationResult(
    TimeSpan Window,
    double WindowHours,
    DateTime DueAtUtc,
    string Reason);

public interface ISlaPolicy
{
    SlaCalculationResult CalculateDue(DateTime startTimeUtc, TicketPriority priority, CustomerTier customerTier);
    double GetAtRiskThresholdPercent();
}

/// <summary>
/// Domain service implementing BR-2, BR-3, and BR-6: Configurable SLA window calculation and customer tier adjustment.
/// </summary>
public sealed class SlaPolicy : ISlaPolicy
{
    private readonly SlaOptions _options;

    public SlaPolicy(IOptions<SlaOptions> options)
    {
        _options = options.Value;
    }

    public SlaPolicy(SlaOptions? options = null)
    {
        _options = options ?? new SlaOptions();
    }

    public SlaCalculationResult CalculateDue(DateTime startTimeUtc, TicketPriority priority, CustomerTier customerTier)
    {
        var priorityKey = priority.ToString();
        var baseHours = _options.BaseWindowHours.TryGetValue(priorityKey, out var configuredHours)
            ? (double)configuredHours
            : priority switch
            {
                TicketPriority.Critical => 4.0,
                TicketPriority.High => 8.0,
                TicketPriority.Medium => 24.0,
                TicketPriority.Low => 72.0,
                _ => 24.0
            };

        double effectiveHours;
        string reason;

        if (customerTier == CustomerTier.Premium)
        {
            var calculated = baseHours * _options.PremiumCustomerMultiplier;
            effectiveHours = Math.Max(calculated, _options.MinimumWindowHours);

            reason = effectiveHours == _options.MinimumWindowHours && calculated < _options.MinimumWindowHours
                ? $"Base window {baseHours:0.#}h halved for Premium customer, clamped to minimum floor of {_options.MinimumWindowHours:0.#}h."
                : $"Base window {baseHours:0.#}h halved to {effectiveHours:0.#}h for Premium customer.";
        }
        else
        {
            effectiveHours = baseHours;
            reason = $"Base window {baseHours:0.#}h applied for Standard customer.";
        }

        var window = TimeSpan.FromHours(effectiveHours);
        var dueAtUtc = startTimeUtc.Add(window);

        return new SlaCalculationResult(window, effectiveHours, dueAtUtc, reason);
    }

    public double GetAtRiskThresholdPercent() => _options.AtRiskThresholdPercent;
}
