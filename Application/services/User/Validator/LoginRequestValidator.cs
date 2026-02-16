using Application.Services.User.Command; // Assuming this is where your LoginCommand lives
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Request.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Invalid email address");

            RuleFor(x => x.Request.Password)
                .NotEmpty()
                .WithMessage("Password is required");
        }
    }
}