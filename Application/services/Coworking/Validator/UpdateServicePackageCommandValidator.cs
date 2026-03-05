using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class UpdateServicePackageCommandValidator
        : AbstractValidator<UpdateServicePackageCommand>
    {
        public UpdateServicePackageCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Package ID is required.");

            When(x => x.Request.Name is not null, () =>
            {
                RuleFor(x => x.Request.Name)
                    .NotEmpty().WithMessage("Package name must not be empty.")
                    .MaximumLength(150).WithMessage("Package name must not exceed 150 characters.");
            });

            When(x => x.Request.PriceAmount.HasValue, () =>
            {
                RuleFor(x => x.Request.PriceAmount)
                    .GreaterThan(0).WithMessage("Price must be greater than 0.");
            });

            When(x => x.Request.StudentDiscountPercent.HasValue, () =>
            {
                RuleFor(x => x.Request.StudentDiscountPercent)
                    .InclusiveBetween(0, 100)
                    .WithMessage("Student discount must be between 0 and 100.");
            });

            When(x => x.Request.DownpaymentPercent.HasValue, () =>
            {
                RuleFor(x => x.Request.DownpaymentPercent)
                    .InclusiveBetween(0, 100)
                    .WithMessage("Downpayment percent must be between 0 and 100.");
            });
        }
    }
}
