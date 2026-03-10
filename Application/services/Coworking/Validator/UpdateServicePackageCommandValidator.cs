using Application.Services.Coworking.Command;
using Domain.Enum;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class UpdateServicePackageCommandValidator
        : AbstractValidator<UpdateServicePackageCommand>
    {
        private static readonly PackageType[] HoursRequiredTypes =
        [
            PackageType.DayPass,
            PackageType.HalfDay,
            PackageType.FullDay,
            PackageType.WeeklyPass,
        ];

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

            // If PackageType is being changed to a hours-based type,
            // DurationHours must be provided and > 0
            When(x => x.Request.PackageType.HasValue &&
                      HoursRequiredTypes.Contains(x.Request.PackageType!.Value), () =>
            {
                RuleFor(x => x.Request.DurationHours)
                    .NotNull().WithMessage("Duration hours is required for this package type.")
                    .GreaterThan(0).WithMessage("Duration hours must be greater than 0.");
            });

            // If PackageType is being changed to MonthlyDedicatedDesk,
            // DurationDays must be provided and > 0
            When(x => x.Request.PackageType == PackageType.MonthlyDedicatedDesk, () =>
            {
                RuleFor(x => x.Request.DurationDays)
                    .NotNull().WithMessage("Duration days is required for Monthly Dedicated Desk.")
                    .GreaterThan(0).WithMessage("Duration days must be greater than 0.");
            });

            // If DurationHours is provided without changing PackageType, it must be > 0
            When(x => !x.Request.PackageType.HasValue && x.Request.DurationHours.HasValue, () =>
            {
                RuleFor(x => x.Request.DurationHours)
                    .GreaterThan(0).WithMessage("Duration hours must be greater than 0.");
            });

            // If DurationDays is provided without changing PackageType, it must be > 0
            When(x => !x.Request.PackageType.HasValue && x.Request.DurationDays.HasValue, () =>
            {
                RuleFor(x => x.Request.DurationDays)
                    .GreaterThan(0).WithMessage("Duration days must be greater than 0.");
            });
        }
    }
}
