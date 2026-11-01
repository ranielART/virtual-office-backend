using Application.Services.Coworking.Command;
using FluentValidation;

namespace Application.Services.Coworking.Validator
{
    public class ValidateManualPaymentCommandValidator
        : AbstractValidator<ValidateManualPaymentCommand>
    {
        public ValidateManualPaymentCommandValidator()
        {
            RuleFor(x => x.PaymentId)
                .NotEmpty().WithMessage("Payment ID is required.");

            RuleFor(x => x.Request.RejectionReason)
                .NotEmpty().WithMessage("A rejection reason is required when declining a payment.")
                .MaximumLength(500).WithMessage("Rejection reason must not exceed 500 characters.")
                .When(x => !x.Request.IsApproved);
        }
    }
}
