using Application.Services.Coworking.Command;
using Domain.Enum;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class CreateCoworkingResourceCommandValidator
        : AbstractValidator<CreateCoworkingResourceCommand>
    {
        public CreateCoworkingResourceCommandValidator()
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("Resource name is required.")
                .MaximumLength(100).WithMessage("Resource name must not exceed 100 characters.");

            RuleFor(x => x.Request.ResourceType)
                .IsInEnum().WithMessage("Invalid resource type.");

            RuleFor(x => x.Request.Capacity)
                .GreaterThanOrEqualTo(1).WithMessage("Capacity must be at least 1.");
        }
    }
}
