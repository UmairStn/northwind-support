using FluentValidation;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummaryReport;

public sealed class SlaSummaryReportQueryValidator : AbstractValidator<SlaSummaryReportQuery>
{
    private static readonly string[] AllowedGroupByValues = ["agent", "priority"];

    public SlaSummaryReportQueryValidator()
    {
        RuleFor(x => x.GroupBy)
            .NotEmpty()
            .WithMessage("GroupBy parameter is required.")
            .Must(groupBy => AllowedGroupByValues.Contains(groupBy?.Trim().ToLowerInvariant()))
            .WithMessage("GroupBy must be either 'agent' or 'priority'.");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From.Value <= x.To.Value)
            .WithMessage("'From' date must be less than or equal to 'To' date.")
            .WithName("DateRange");
    }
}
