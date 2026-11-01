using Application.Services.User.Command; // 
using FluentValidation;

namespace Application.Services.User.Validator
{
    // 1. Change target from 'CreateAdminRequest' to 'CreateAdminCommand'
    public class CreateAdminCommandValidator : AbstractValidator<CreateAdminCommand>
    {
        public CreateAdminCommandValidator()
        {
            // 2. Add '.Request' to every rule
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
                .MinimumLength(10).WithMessage("Admin passwords must be at least 10 characters long.")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least one number.");

            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required")
                .MinimumLength(10).WithMessage("Phone must be exactly 10 characters long.");

            // 3. This is the logic that was failing
            RuleFor(x => x.Request.ConfirmPassword)
                .Equal(x => x.Request.Password).WithMessage("Passwords do not match.");
        }
    }
}