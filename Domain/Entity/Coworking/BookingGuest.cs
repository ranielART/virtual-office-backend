using Domain.Common;

namespace Domain.Entity.Coworking
{
    public class BookingGuest : GuidBaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Phone { get; set; } = string.Empty;

        // Student verification
        public string? StudentIdImageUrl { get; set; }
        public bool IsStudentVerified { get; set; }

        // FK — dependent side of the 1-to-1 with Booking
        public Guid BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
    }
}
