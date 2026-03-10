using Application.Services.Coworking.Query;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class CheckWalkInAvailabilityQueryValidator : AbstractValidator<CheckWalkInAvailabilityQuery>
    {
        public CheckWalkInAvailabilityQueryValidator()
        {
            RuleFor(x => x.ResourceId)
                .NotEmpty()
                .WithMessage("Resource ID is required.");

            RuleFor(x => x.EstimatedHours)
                .GreaterThan(0).WithMessage("Estimated hours must be greater than 0.")
                .LessThanOrEqualTo(24).WithMessage("Estimated hours must not exceed 24.");
        }
    }
}
