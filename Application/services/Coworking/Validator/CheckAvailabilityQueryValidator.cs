using Application.Services.Coworking.Query;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class CheckAvailabilityQueryValidator : AbstractValidator<CheckAvailabilityQuery>
    {
        public CheckAvailabilityQueryValidator()
        {
            RuleFor(x => x.Date)
                .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
                .WithMessage("Date must not be in the past.");

            RuleFor(x => x)
                .Must(q => q.StartTime < q.EndTime)
                .WithMessage("Start time must be before end time.");

            RuleFor(x => x)
                .Must(q => (q.Date.ToDateTime(q.EndTime) - q.Date.ToDateTime(q.StartTime)).TotalHours <= 24)
                .WithMessage("Time window must be 24 hours or less.");

            RuleFor(x => x.ResourceType)
                .IsInEnum()
                .When(x => x.ResourceType.HasValue)
                .WithMessage("Invalid resource type.");
        }
    }
}
