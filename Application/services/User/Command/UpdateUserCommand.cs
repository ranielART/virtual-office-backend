using Application.Services.User.Request;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;
using FluentValidation;
using FluentValidation.Results;
namespace Application.Services.User.Command
{


    public class UpdateUserCommand : IRequest<UpdateUserResponse>
    {
        public string Id { get; set; } // Added here to hold the URL ID
        public UpdateUserRequest Request { get; set; }

        public UpdateUserCommand(string id, UpdateUserRequest request)
        {
            Id = id;
            Request = request;
        }
    }

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public UpdateUserCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<UpdateUserResponse> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
        {
            // Use the ID from the command (which came from the URL)
            var user = await _userManager.FindByIdAsync(command.Id);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {command.Id} not found.");
            }

            var request = command.Request;

            // 1. Update Basic Info
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Address = request.Address;
            user.PhoneNumber = request.PhoneNumber;

            // 2. Handle Email Changes (if strictly necessary)
            if (request.Email != user.Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    throw new InvalidOperationException("Email is already in use.");
                }
                user.Email = request.Email;
                user.UserName = request.Email;
                await _userManager.UpdateNormalizedEmailAsync(user);
                await _userManager.UpdateNormalizedUserNameAsync(user);
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                // 1. Convert Identity Errors to FluentValidation Failures
                var failures = result.Errors
                    .Select(error => new ValidationFailure(
                        propertyName: error.Code, // Or use "Password" if you prefer generic keys
                        errorMessage: error.Description))
                    .ToList();

                // 2. Throw the exception that your GlobalExceptionHandler knows how to format
                throw new ValidationException(failures);
            }

            return new UpdateUserResponse { Id = user.Id, Message = "User updated successfully." };
        }
    }
}