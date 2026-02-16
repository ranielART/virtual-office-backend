using Application.Services.User.Request;
using Application.Services.User.Response;
using AutoMapper;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Identity;

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
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create user: {errors}");
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
