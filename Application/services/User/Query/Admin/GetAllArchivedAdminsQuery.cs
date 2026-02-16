using Application.Services.User.Query.User;

using MediatR;
using Microsoft.AspNetCore.Identity;


namespace Application.Services.User.Query.Admin
{
    public class GetAllArchivedAdminsQuery : IRequest<List<UserDto>> { }

    public class GetAllArchivedAdminsQueryHandler : IRequestHandler<GetAllArchivedAdminsQuery, List<UserDto>>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public GetAllArchivedAdminsQueryHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<UserDto>> Handle(GetAllArchivedAdminsQuery request, CancellationToken cancellationToken)
        {
            // Get all users who are IN the 'Admin' role AND are NOT Active
            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            // Filter for Inactive
            var archivedAdmins = admins.Where(u => !u.IsActive).ToList();

            var userDtos = new List<UserDto>();
            foreach (var user in archivedAdmins)
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
