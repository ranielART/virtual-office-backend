using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class UpdateCoworkingResourceCommandValidator
        : AbstractValidator<UpdateCoworkingResourceCommand>
    {
        public UpdateCoworkingResourceCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Resource ID is required.");

            When(x => x.Request.Name is not null, () =>
            {
                RuleFor(x => x.Request.Name)
                    .NotEmpty().WithMessage("Resource name must not be empty.")
                    .MaximumLength(100).WithMessage("Resource name must not exceed 100 characters.");
            });

            When(x => x.Request.Capacity.HasValue, () =>
            {
                RuleFor(x => x.Request.Capacity)
                    .GreaterThanOrEqualTo(1).WithMessage("Capacity must be at least 1.");
            });

        }
    }
}
