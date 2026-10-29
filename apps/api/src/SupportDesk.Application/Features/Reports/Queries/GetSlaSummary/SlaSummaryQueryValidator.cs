using FluentValidation;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

/// <summary>
/// groupBy is required and must name a grouping; a date range, when both ends are given, must
/// not be empty or backwards.
/// </summary>
public sealed class SlaSummaryQueryValidator : AbstractValidator<SlaSummaryQuery>
{
    public SlaSummaryQueryValidator()
    {
        RuleFor(x => x.GroupBy)
            .Must(g => string.Equals(g, "agent", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(g, "priority", StringComparison.OrdinalIgnoreCase))
            .WithMessage("groupBy must be 'agent' or 'priority'.");

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("to must be later than from.");
    }
}
