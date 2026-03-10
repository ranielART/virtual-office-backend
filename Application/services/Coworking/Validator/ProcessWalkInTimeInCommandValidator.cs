using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class ProcessWalkInTimeInCommandValidator : AbstractValidator<ProcessWalkInTimeInCommand>
    {
        public ProcessWalkInTimeInCommandValidator()
        {
            RuleFor(x => x.Request.ResourceId)
                .NotEmpty().WithMessage("Resource ID is required.");

            RuleFor(x => x.Request.ServicePackageId)
                .NotEmpty().WithMessage("Service package ID is required.");

            RuleFor(x => x.Request.EstimatedHours)
                .GreaterThan(0).WithMessage("Estimated hours must be greater than 0.")
                .LessThanOrEqualTo(24).WithMessage("Estimated hours must not exceed 24.");

            RuleFor(x => x.Request.GuestFirstName)
                .NotEmpty().WithMessage("Guest first name is required.")
                .MaximumLength(100).WithMessage("Guest first name must not exceed 100 characters.");

            RuleFor(x => x.Request.GuestLastName)
                .NotEmpty().WithMessage("Guest last name is required.")
                .MaximumLength(100).WithMessage("Guest last name must not exceed 100 characters.");

            RuleFor(x => x.Request.GuestPhone)
                .NotEmpty().WithMessage("Guest phone number is required.")
                .Matches(@"^\d{10,11}$").WithMessage("Phone number must be 10 or 11 digits.");

            RuleFor(x => x.Request.GuestEmail)
                .EmailAddress().WithMessage("Guest email must be a valid email address.")
                .When(x => !string.IsNullOrWhiteSpace(x.Request.GuestEmail));
        }
    }
}
