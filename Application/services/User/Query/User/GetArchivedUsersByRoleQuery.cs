using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Query.User
{
    public class GetArchivedUsersByRoleQuery : IRequest<List<UserDto>>
    {
        public string Role { get; set; }

        public GetArchivedUsersByRoleQuery(string role)
        {
            Role = role;
        }
    }

    public class GetArchivedUsersByRoleQueryHandler : IRequestHandler<GetArchivedUsersByRoleQuery, List<UserDto>>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public GetArchivedUsersByRoleQueryHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<UserDto>> Handle(GetArchivedUsersByRoleQuery request, CancellationToken cancellationToken)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(request.Role);
            var archivedUsers = usersInRole.Where(u => !u.IsActive).ToList();

            var userDtos = new List<UserDto>();
            foreach (var user in archivedUsers)
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
