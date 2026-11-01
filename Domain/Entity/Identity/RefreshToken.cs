using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entity.Identity
{
    public class RefreshToken
    {
        public Guid Id { get; set; } // Primary Key

        public string Token { get; set; } = string.Empty; // The actual secure string
        public string JwtId { get; set; } = string.Empty; // Links to the Access Token (JTI)

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime ExpiryDate { get; set; }

        // Security Tracking
        public bool Used { get; set; } // Has this token been used? (For Rotation)
        public bool Invalidated { get; set; } // Was this revoked manually?

        // Relationship
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = default!;


    }
}
