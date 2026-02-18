using Application.Common.Exceptions;
using Application.Services.User.Request;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class ResetPasswordCommand : IRequest<Unit>
    {
        public string TargetUserId { get; set; }
        public ResetPasswordRequest Request { get; set; }

        public ResetPasswordCommand(string targetUserId, ResetPasswordRequest request)
        {
            TargetUserId = targetUserId;
            Request = request;
        }
    }

    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public ResetPasswordCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.TargetUserId)
                ?? throw new NotFoundException($"User with ID '{command.TargetUserId}' not found.");

            // Remove the existing password and set the new one directly (no current password needed)
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                var failures = removeResult.Errors
                    .Select(e => new ValidationFailure(e.Code, e.Description))
                    .ToList();
                throw new ValidationException(failures);
            }

            var addResult = await _userManager.AddPasswordAsync(user, command.Request.NewPassword);
            if (!addResult.Succeeded)
            {
                var failures = addResult.Errors
                    .Select(e => new ValidationFailure(e.Code, e.Description))
                    .ToList();
                throw new ValidationException(failures);
            }

            // Force the user to change the admin-assigned password on next login
            user.RequiresPasswordChange = true;
            await _userManager.UpdateAsync(user);

            return Unit.Value;
        }
    }
}
