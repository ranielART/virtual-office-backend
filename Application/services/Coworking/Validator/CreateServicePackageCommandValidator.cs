using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class CreateServicePackageCommandValidator
        : AbstractValidator<CreateServicePackageCommand>
    {
        public CreateServicePackageCommandValidator()
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("Package name is required.")
                .MaximumLength(150).WithMessage("Package name must not exceed 150 characters.");

            RuleFor(x => x.Request.ResourceType)
                .IsInEnum().WithMessage("Invalid resource type.");

            RuleFor(x => x.Request.PackageType)
                .IsInEnum().WithMessage("Invalid package type.");

            RuleFor(x => x.Request.PriceAmount)
                .GreaterThan(0).WithMessage("Price must be greater than 0.");

            RuleFor(x => x.Request.StudentDiscountPercent)
                .InclusiveBetween(0, 100)
                .WithMessage("Student discount must be between 0 and 100.");

            RuleFor(x => x.Request.DownpaymentPercent)
                .InclusiveBetween(0, 100)
                .WithMessage("Downpayment percent must be between 0 and 100.");
        }
    }
}
