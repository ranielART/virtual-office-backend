using Application.Common.Exceptions;
using Application.Services.User.Request;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class ChangePasswordCommand : IRequest<Unit>
    {
        public string UserId { get; set; }
        public ChangePasswordRequest Request { get; set; }

        public ChangePasswordCommand(string userId, ChangePasswordRequest request)
        {
            UserId = userId;
            Request = request;
        }
    }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public ChangePasswordCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.UserId)
                ?? throw new NotFoundException("User not found.");

            var result = await _userManager.ChangePasswordAsync(
                user,
                command.Request.CurrentPassword,
                command.Request.NewPassword);

            if (!result.Succeeded)
            {
                var failures = result.Errors
                    .Select(e => new ValidationFailure(e.Code, e.Description))
                    .ToList();
                throw new ValidationException(failures);
            }

            // Clear the forced password change flag once they've set their own
            user.RequiresPasswordChange = false;
            await _userManager.UpdateAsync(user);

            return Unit.Value;
        }
    }
}
