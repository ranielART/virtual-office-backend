using Application.Services.User.Request;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Domain.Enum;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.User.Command
{
    // Command wraps the Request
    public class CreateAdminCommand : IRequest<CreateAdminResponse>
    {
        public CreateAdminRequest Request { get; set; }

        public CreateAdminCommand(CreateAdminRequest request)
        {
            Request = request;
        }
    }

    public class CreateAdminCommandHandler : IRequestHandler<CreateAdminCommand, CreateAdminResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public CreateAdminCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<CreateAdminResponse> Handle(CreateAdminCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;

            // 1. Check if user exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException($"User with email {request.Email} already exists.");
            }

            // 2. Create User Entity
            var user = new Domain.Entity.Identity.User
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                //Address = request.Address,

                // Admins are automatically approved/active
                Status = RegistrationStatus.Approved,
                EmailConfirmed = true,
                RequiresPasswordChange = true // Force them to change the password you gave them
            };

            // 3. Save to DB with Password
            var result = await _userManager.CreateAsync(user, request.Password);

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

            // 4. Assign "Admin" Role (CRITICAL STEP)
            var roleResult = await _userManager.AddToRoleAsync(user, "Admin");

            if (!roleResult.Succeeded)
            {
                // Rollback: If role assignment fails, delete the user so we don't have a floating user
                await _userManager.DeleteAsync(user);
                throw new InvalidOperationException("Failed to assign Admin role.");
            }

            return new CreateAdminResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Message = "Admin account created successfully."
            };
        }
    }
}