using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enum
{
    public enum RegistrationStatus
    {
        Pending = 1, // Default for new registrations
        Active = 2,  // Approved by Admin
        Rejected = 3
    }
}
