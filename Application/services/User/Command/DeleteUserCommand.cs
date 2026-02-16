using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{

    public class DeleteUserCommand : IRequest<DeleteUserResponse>
    {
        public string Id { get; set; }
        public DeleteUserCommand(string id) => Id = id;
    }

    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, DeleteUserResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public DeleteUserCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<DeleteUserResponse> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.Id);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {command.Id} not found.");

            // 1. Soft Delete (Using the IsActive field you added)
            // If you are using the Status enum, use: user.Status = Domain.Enum.RegistrationStatus.Deactivated;
            user.IsActive = false;

            // 2. Security: Lock them out immediately
            // This ensures they cannot log in again even if they have a valid password
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException("Failed to deactivate user.");

            return new DeleteUserResponse { Id = user.Id, Message = "User deactivated successfully." };
        }
    }
}