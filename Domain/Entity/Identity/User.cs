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
        public ICollection<UserRole> Roles { get; set; } = default!;

    }
}