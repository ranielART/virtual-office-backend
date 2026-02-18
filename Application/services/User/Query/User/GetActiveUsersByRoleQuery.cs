using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Query.User
{
    public class GetActiveUsersByRoleQuery : IRequest<List<UserDto>>
    {
        public string Role { get; set; }

        public GetActiveUsersByRoleQuery(string role)
        {
            Role = role;
        }
    }

    public class GetActiveUsersByRoleQueryHandler : IRequestHandler<GetActiveUsersByRoleQuery, List<UserDto>>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public GetActiveUsersByRoleQueryHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<UserDto>> Handle(GetActiveUsersByRoleQuery request, CancellationToken cancellationToken)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(request.Role);
            var activeUsers = usersInRole.Where(u => u.IsActive).ToList();

            var userDtos = new List<UserDto>();
            foreach (var user in activeUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber!,
                    Address = user.Address,
                    IsActive = user.IsActive,
                    Roles = roles
                });
            }

            return userDtos;
        }
    }
}
