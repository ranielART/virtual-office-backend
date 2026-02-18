using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class CreateFrontDeskCommandValidator : AbstractValidator<CreateFrontDeskCommand>
    {
        public CreateFrontDeskCommandValidator()
        {
            RuleFor(x => x.Request.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(2, 50).WithMessage("First name must be between 2 and 50 characters");

            RuleFor(x => x.Request.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(2, 50).WithMessage("Last name must be between 2 and 50 characters");

            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.Request.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least one number.");

            RuleFor(x => x.Request.ConfirmPassword)
                .NotEmpty().WithMessage("Password confirmation is required")
                .Equal(x => x.Request.Password).WithMessage("Passwords do not match.");

            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required")
                .Length(10).WithMessage("Phone number must be exactly 10 digits.")
                .Matches(@"^[0-9]+$").WithMessage("Phone number must contain only digits.");
        }
    }
}
