using Application.Services.Public.Command;
using FluentValidation;

namespace Application.Services.Public.Validator
{
    public class SendEmailCommandValidator : AbstractValidator<SendEmailCommand>
    {
        public SendEmailCommandValidator()
        {
            RuleFor(x => x.Request.FullName)
                .NotEmpty().WithMessage("Full name is required.")
                .MaximumLength(150).WithMessage("Full name must not exceed 150 characters.");

            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("A valid email address is required.")
                .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

            RuleFor(x => x.Request.Subject)
                .NotEmpty().WithMessage("Subject is required.")
                .MaximumLength(200).WithMessage("Subject must not exceed 200 characters.");

            RuleFor(x => x.Request.Message)
                .NotEmpty().WithMessage("Message is required.")
                .MaximumLength(4000).WithMessage("Message must not exceed 4000 characters.");

            RuleFor(x => x.Request.Phone)
                .Matches(@"^\d{10,11}$").WithMessage("Phone number must be 10 or 11 digits.")
                .When(x => !string.IsNullOrWhiteSpace(x.Request.Phone));
        }
    }
}