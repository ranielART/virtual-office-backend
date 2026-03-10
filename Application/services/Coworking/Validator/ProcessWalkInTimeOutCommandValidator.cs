using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class ProcessWalkInTimeOutCommandValidator : AbstractValidator<ProcessWalkInTimeOutCommand>
    {
        public ProcessWalkInTimeOutCommandValidator()
        {
            RuleFor(x => x.ActualEndOverride)
                .LessThanOrEqualTo(_ => DateTime.UtcNow)
                .WithMessage("Actual end time cannot be in the future.")
                .When(x => x.ActualEndOverride.HasValue);

            RuleFor(x => x.TimeOutNotes)
                .MaximumLength(500)
                .WithMessage("Time-out notes must not exceed 500 characters.")
                .When(x => !string.IsNullOrWhiteSpace(x.TimeOutNotes));
        }
    }
}
