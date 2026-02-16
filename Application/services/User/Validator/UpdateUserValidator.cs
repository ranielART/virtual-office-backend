using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserValidator()
        {
            // Validating the Route Parameter
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("User ID is required.");

            // Validating the Body Fields
            RuleFor(x => x.Request.FirstName)
                .NotEmpty().WithMessage("First Name is required.")
                .MaximumLength(50);

            RuleFor(x => x.Request.LastName)
                .NotEmpty().WithMessage("Last Name is required.")
                .MaximumLength(50);

            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress();

            // Your specific 10-digit, no-leading-zero rule
            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Phone Number is required.")
                .Length(10).WithMessage("Phone Number must be exactly 10 digits.")
                .Matches(@"^[1-9][0-9]{9}$").WithMessage("Phone Number cannot start with 0.");
        }
    }
}