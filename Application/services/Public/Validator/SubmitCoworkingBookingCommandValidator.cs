using Application.Common.Helpers;
using Application.Services.Public.Command;
using FluentValidation;

namespace Application.Services.Public.Validator
{
    public class SubmitCoworkingBookingCommandValidator
        : AbstractValidator<SubmitCoworkingBookingCommand>
    {
        public SubmitCoworkingBookingCommandValidator()
        {
            RuleFor(x => x.Request.ServicePackageId)
                .NotEmpty().WithMessage("Service package is required.");

            RuleFor(x => x.Request.Date)
                .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Booking date cannot be in the past.");

            RuleFor(x => x.Request.StartTime)
                .NotEqual(default(TimeOnly)).WithMessage("Start time is required.");

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
