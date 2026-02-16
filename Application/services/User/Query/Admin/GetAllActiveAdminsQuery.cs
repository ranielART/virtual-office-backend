using Application.Services.User.Query.User;

using MediatR;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.User.Query.Admin
{
    public class GetAllActiveAdminsQuery : IRequest<List<UserDto>> { }

    public class GetAllActiveAdminsQueryHandler : IRequestHandler<GetAllActiveAdminsQuery, List<UserDto>>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public GetAllActiveAdminsQueryHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<UserDto>> Handle(GetAllActiveAdminsQuery request, CancellationToken cancellationToken)
        {
            // Get all users who are IN the 'Admin' role AND are Active
            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            // Filter in memory (GetUsersInRoleAsync returns a list, not IQueryable)
            var activeAdmins = admins.Where(u => u.IsActive).ToList();

            var userDtos = new List<UserDto>();
            foreach (var user in activeAdmins)
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
