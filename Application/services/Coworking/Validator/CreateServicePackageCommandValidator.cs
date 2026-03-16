using Application.Services.Coworking.Command;
using Domain.Enum;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class CreateServicePackageCommandValidator
        : AbstractValidator<CreateServicePackageCommand>
    {
        private static readonly PackageType[] HoursRequiredTypes =
        [
            PackageType.DayPass,
            PackageType.HalfDay,
            PackageType.FullDay,
            PackageType.WeeklyPass,
        ];

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

            // DurationHours required for flat hour-based package types
            RuleFor(x => x.Request.DurationHours)
                .NotNull().WithMessage("Duration hours is required for this package type.")
                .GreaterThan(0).WithMessage("Duration hours must be greater than 0.")
                .When(x => HoursRequiredTypes.Contains(x.Request.PackageType));

            // DurationDays required for monthly desk
            RuleFor(x => x.Request.DurationDays)
                .NotNull().WithMessage("Duration days is required for Monthly Dedicated Desk.")
                .GreaterThan(0).WithMessage("Duration days must be greater than 0.")
                .When(x => x.Request.PackageType == PackageType.MonthlyDedicatedDesk);

            // At least one payment scheme must be enabled
            RuleFor(x => x.Request)
                .Must(r => r.AllowsUpfront || r.AllowsPostpaid || r.AllowsDaily)
                .WithMessage("At least one payment scheme (AllowsUpfront, AllowsPostpaid, AllowsDaily) must be enabled.");

            // AllowsUpfront is not valid for Hourly — total is unknown at time-in
            RuleFor(x => x.Request.AllowsUpfront)
                .Equal(false)
                .WithMessage("Hourly packages cannot allow upfront payment — the total is unknown at time-in.")
                .When(x => x.Request.PackageType == PackageType.Hourly);

            // AllowsDaily is only valid for MonthlyDedicatedDesk
            RuleFor(x => x.Request.AllowsDaily)
                .Equal(false)
                .WithMessage("Daily payment scheme is only available for Monthly Dedicated Desk packages.")
                .When(x => x.Request.PackageType != PackageType.MonthlyDedicatedDesk);
        }
    }
}
