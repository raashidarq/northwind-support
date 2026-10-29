using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Lengths are checked on the trimmed text, because that is what is stored. The web app's
/// escalation form applies the same limits.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;

    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(r => r.Trim().Length is >= ReasonMinLength and <= ReasonMaxLength)
            .WithMessage($"Reason must be between {ReasonMinLength} and {ReasonMaxLength} characters.");
    }
}
