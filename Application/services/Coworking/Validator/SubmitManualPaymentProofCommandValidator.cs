using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class SubmitManualPaymentProofCommandValidator
        : AbstractValidator<SubmitManualPaymentProofCommand>
    {
        public SubmitManualPaymentProofCommandValidator()
        {
            RuleFor(x => x.Request.BookingId)
                .NotEmpty().WithMessage("Booking ID is required.");

            RuleFor(x => x.Request.Amount)
                .GreaterThan(0).WithMessage("Payment amount must be greater than 0.");

            RuleFor(x => x.Request.PaymentType)
                .IsInEnum().WithMessage("Invalid payment type.");

            RuleFor(x => x.File)
                .NotNull().WithMessage("A proof of payment image is required.");

            When(x => x.File != null, () =>
            {
                RuleFor(x => x.File.ContentType)
                    .Must(ct => ct == "image/jpeg" || ct == "image/png")
                    .WithMessage("Only JPEG and PNG images are accepted.");

                RuleFor(x => x.File.Length)
                    .LessThanOrEqualTo(5 * 1024 * 1024)
                    .WithMessage("File must not exceed 5 MB.");
            });
        }
    }
}
