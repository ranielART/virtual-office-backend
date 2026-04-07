using Application.Common.Helpers;
using Application.Services.Public.Command;
using FluentValidation;

namespace Application.Services.Public.Validator
{
    public class SubmitCoworkingBookingCommandValidator
        : AbstractValidator<SubmitCoworkingBookingCommand>
    {
        private static readonly HashSet<string> ValidResourcePackages =
        [
            "Coworking Seat: Hourly Pass",
            "Coworking Seat: Day Pass",
            "Coworking Seat: Weekly Pass",
            "Coworking Seat: Monthly Dedicated Desk",
            "Executive Meeting Room: Hourly",
            "Executive Meeting Room: Half-Day (5 hrs)",
            "Executive Meeting Room: Full Day (8 hrs)",
            "Collaboration Room: Hourly",
            "Collaboration Room: Half-Day (4 hrs)",
            "Collaboration Room: Full Day (8 hrs)",
        ];

        public SubmitCoworkingBookingCommandValidator()
        {
            RuleFor(x => x.Request.ResourcePackage)
                .NotEmpty().WithMessage("Resource package selection is required.")
                .Must(v => ValidResourcePackages.Contains(v))
                .WithMessage("The selected package does not exist. Please select a valid package.");

            RuleFor(x => x.Request.Date)
                .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Booking date cannot be in the past.");

            RuleFor(x => x.Request.StartTime)
                .NotEqual(default(TimeOnly)).WithMessage("Start time is required.");

            // EndTime is required for hourly packages
            RuleFor(x => x.Request.EndTime)
                .NotNull().WithMessage("End time is required for hourly packages.")
                .When(x => PriceCalculator.IsHourlyCoworkingPackage(x.Request.ResourcePackage));

            // EndTime must be after StartTime when provided
            RuleFor(x => x)
                .Must(x => !x.Request.EndTime.HasValue || x.Request.EndTime.Value > x.Request.StartTime)
                .WithMessage("End time must be after start time.")
                .When(x => x.Request.EndTime.HasValue);

            // Hourly duration must not exceed 24 hours
            RuleFor(x => x)
                .Must(x =>
                {
                    if (!x.Request.EndTime.HasValue) return true;
                    var hours = (x.Request.EndTime.Value - x.Request.StartTime).TotalHours;
                    return hours > 0 && hours <= 24;
                })
                .WithMessage("Booking duration must be between 0 and 24 hours.")
                .When(x => PriceCalculator.IsHourlyCoworkingPackage(x.Request.ResourcePackage)
                            && x.Request.EndTime.HasValue);

            RuleFor(x => x.Request.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("A valid email address is required.");

            RuleFor(x => x.Request.PhoneCountryCode)
                .NotEmpty().WithMessage("Phone country code is required.")
                .Matches(@"^\+\d{1,4}$").WithMessage("Phone country code must be in the format '+XX' (e.g. '+63').");

            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^\d{7,12}$").WithMessage("Phone number must be between 7 and 12 digits.");

            // Payment screenshots
            RuleFor(x => x.PaymentScreenshots)
                .NotEmpty().WithMessage("At least one payment screenshot is required.")
                .Must(f => f.Count <= 2).WithMessage("Maximum 2 payment screenshots are allowed.");

            RuleForEach(x => x.PaymentScreenshots)
                .ChildRules(file =>
                {
                    file.RuleFor(f => f.Length)
                        .LessThanOrEqualTo(5 * 1024 * 1024)
                        .WithMessage("Each screenshot must not exceed 5 MB.");

                    file.RuleFor(f => f.ContentType)
                        .Must(ct => FileSecurityHelper.AllowedImageContentTypes.Contains(ct))
                        .WithMessage("Only JPEG, PNG, and WebP images are accepted.");

                    file.RuleFor(f => f)
                        .Must(f =>
                        {
                            using var stream = f.OpenReadStream();
                            return FileSecurityHelper.VerifyMagicBytes(stream, f.ContentType);
                        })
                        .WithMessage("File content does not match the declared file type.");
                });
        }
    }
}
