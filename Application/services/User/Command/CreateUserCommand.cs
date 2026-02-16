using Application.Services.User.Request;
using Application.Services.User.Response;
using AutoMapper;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Identity;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.User.Command
{
    public class CreateUserCommand : IRequest<CreateUserResponse>
    {
        public CreateUserRequest Request { get; set; }

        public CreateUserCommand(CreateUserRequest request)
        {
            Request = request;
        }
    }

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly IMapper _mapper;

        public CreateUserCommandHandler(UserManager<Domain.Entity.Identity.User> userManager, IMapper mapper)
        {
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<CreateUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var createUserRequest = request.Request;

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(createUserRequest.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("User with this email already exists");
            }

            // Create new user
            var user = new Domain.Entity.Identity.User
            {
                UserName = createUserRequest.Email,
                Email = createUserRequest.Email,
                FirstName = createUserRequest.FirstName,
                PhoneNumber = createUserRequest.PhoneNumber, 
                LastName = createUserRequest.LastName,
                Address = createUserRequest.Address,
                Status = RegistrationStatus.Pending,
                EmailConfirmed = false,
                RequiresPasswordChange = true // Force password change on first login
            };

            // Create user with password
            var result = await _userManager.CreateAsync(user, createUserRequest.Password);

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

            // Return response
            return new CreateUserResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Message = "User created successfully. Your status is pending approval."
            };
        }
    }
}
