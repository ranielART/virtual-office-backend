using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.Request.AccessToken)
                .NotEmpty().WithMessage("Access token is required");

            RuleFor(x => x.Request.RefreshToken)
                .NotEmpty().WithMessage("Refresh token is required");
        }
    }
}
