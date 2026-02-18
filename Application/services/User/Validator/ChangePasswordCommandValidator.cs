using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {
            RuleFor(x => x.Request.CurrentPassword)
                .NotEmpty().WithMessage("Current password is required.");

            RuleFor(x => x.Request.NewPassword)
                .NotEmpty().WithMessage("New password is required.")
                .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least one number.")
                .NotEqual(x => x.Request.CurrentPassword).WithMessage("New password must be different from the current password.");

            RuleFor(x => x.Request.ConfirmNewPassword)
                .NotEmpty().WithMessage("Password confirmation is required.")
                .Equal(x => x.Request.NewPassword).WithMessage("Passwords do not match.");
        }
    }
}
