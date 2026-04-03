namespace Application.Services.Public.Request
{
    /// <summary>
    /// Multipart/form-data request for a public online coworking booking.
    ///
    /// Date and times are expected in UTC.
    /// Files (payment_screenshots) are read separately from Request.Form.Files
    /// in the controller and passed into the command directly.
    /// </summary>
    public class SubmitCoworkingBookingRequest
    {
        /// <summary>ID of the chosen service package (from GET /api/public/packages).</summary>
        public Guid ServicePackageId { get; set; }

        /// <summary>Desired booking date in UTC. Format: YYYY-MM-DD.</summary>
        public DateOnly Date { get; set; }

        /// <summary>Booking start time in UTC. Format: HH:mm.</summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>
        /// Booking end time in UTC. Format: HH:mm.
        /// Required for Hourly packages. Ignored for fixed-duration packages
        /// (ScheduledEnd is derived from the package's DurationHours).
        /// </summary>
        public TimeOnly? EndTime { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>E.g. "+63"</summary>
        public string PhoneCountryCode { get; set; } = string.Empty;

        /// <summary>E.g. "9123456789"</summary>
        public string PhoneNumber { get; set; } = string.Empty;

        public bool IsStudent { get; set; }
    }
}
