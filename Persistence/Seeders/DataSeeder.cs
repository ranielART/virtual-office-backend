using Domain.Entity.Identity;
using Domain.Enum;
using Microsoft.AspNetCore.Identity;
using Persistence.Data;

namespace Persistence.Seeders
{
    public class DataSeeder : IDataSeeder
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager; 
        private readonly ApplicationDbContext _context;

        public DataSeeder(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task SeedAsync()
        {
            // 1. Seed Roles (Admin, Customer, Frontdesk)
            await SeedRolesAsync();

            // 2. Seed Admin User
            await SeedAdminUserAsync();
        }

        private async Task SeedRolesAsync()
        {
            var roles = new List<string> { "Admin", "Customer", "Frontdesk" };

            foreach (var roleName in roles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var role = new Role
                    {
                        Id = Guid.NewGuid().ToString(), // 1. Explicitly set ID to prevent null issues
                        Name = roleName,
                        NormalizedName = roleName.ToUpper()
                    };

                    // 2. Capture the result
                    var result = await _roleManager.CreateAsync(role);

                    // 3. STOP if it fails (This reveals the silent error)
                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        throw new Exception($"Failed to seed role '{roleName}': {errors}");
                    }
                }
            }
        }

        private async Task SeedAdminUserAsync()
        {
            var adminEmail = "admin@virtualoffice.com";

            // Check if admin exists in 'Users' table
            var adminUser = await _userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Administrator",
                    Address = "Virtual Office HQ",

                    // Admin specific settings
                    Status = RegistrationStatus.Approved, // Can login immediately
                    EmailConfirmed = true,
                    RequiresPasswordChange = false // Admin doesn't need to change own pass
                };

                // 1. Create User in DB with password
                // Note: Using a Strong Password to pass validation
                var result = await _userManager.CreateAsync(adminUser, "Admin@123!");

                if (result.Succeeded)
                {
                    // 2. Assign Role (This fills the 'UserRoles' table automatically!)
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}