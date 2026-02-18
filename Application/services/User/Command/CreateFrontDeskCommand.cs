using Application.Services.User.Request;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Domain.Enum;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.User.Command
{
    public class CreateFrontDeskCommand : IRequest<CreateFrontDeskResponse>
    {
        public CreateFrontDeskRequest Request { get; set; }

        public CreateFrontDeskCommand(CreateFrontDeskRequest request)
        {
            Request = request;
        }
    }

    public class CreateFrontDeskCommandHandler : IRequestHandler<CreateFrontDeskCommand, CreateFrontDeskResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public CreateFrontDeskCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<CreateFrontDeskResponse> Handle(CreateFrontDeskCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException($"User with email {request.Email} already exists.");
            }

            var user = new Domain.Entity.Identity.User
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Status = RegistrationStatus.Approved,
                EmailConfirmed = true,
                RequiresPasswordChange = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var failures = result.Errors
                    .Select(error => new ValidationFailure(
                        propertyName: error.Code,
                        errorMessage: error.Description))
                    .ToList();

                throw new ValidationException(failures);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Frontdesk");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                throw new InvalidOperationException("Failed to assign Frontdesk role.");
            }

            return new CreateFrontDeskResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Message = "Frontdesk account created successfully."
            };
        }
    }
}
