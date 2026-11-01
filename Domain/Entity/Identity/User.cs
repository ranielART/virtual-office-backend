using Domain.Enum;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entity.Identity
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; } = String.Empty;
        public string LastName { get; set; } = String.Empty;
        public string MiddleName { get; set; } = String.Empty;
        public string Address { get; set; } = String.Empty;

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public bool RequiresPasswordChange { get; set; } = false; // since the admin generates the pass first, the user must change it on first login

        public ICollection<UserRole> Roles { get; set; } = default!;

        public Boolean IsActive { get; set; } = true; // For soft deletion or deactivation by admin

    }
}