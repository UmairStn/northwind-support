using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Validates reason length (5-500) and non-empty escalatedBy actor.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("An escalation reason is required.")
            .MinimumLength(5).WithMessage("The escalation reason must be between 5 and 500 characters.")
            .MaximumLength(500).WithMessage("The escalation reason must be between 5 and 500 characters.");

        RuleFor(x => x.EscalatedBy)
            .NotEmpty().WithMessage("The actor performing the escalation is required.")
            .MaximumLength(100).WithMessage("The actor name cannot exceed 100 characters.");
    }
}
