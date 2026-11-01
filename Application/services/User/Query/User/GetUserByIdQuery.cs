using Application.Services.User.Query.User;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Query.User
{
    public class GetUserByIdQuery : IRequest<UserDto>
    {
        public string Id { get; set; }
        public GetUserByIdQuery(string id) => Id = id;
    }

    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public GetUserByIdQueryHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<UserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(query.Id);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {query.Id} not found.");

            // Fetch roles to display in the UI
            var roles = await _userManager.GetRolesAsync(user);

            return new UserDto
            { 
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber!,
                Address = user.Address,
                IsActive = user.IsActive, // (Or user.Status == RegistrationStatus.Approved)
                Roles = roles
            };
        }
    }
}