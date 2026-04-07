namespace Application.Services.Public.Request
{
    /// <summary>
    /// Multipart/form-data request for a public online coworking booking inquiry.
    /// Pricing is computed server-side from ResourcePackage — no ServicePackageId needed.
    /// Files (payment_screenshots) are read separately from Request.Form.Files
    /// in the controller and passed into the command directly.
    /// </summary>
    public class SubmitCoworkingBookingRequest
    {
        /// <summary>
        /// Human-readable package label hardcoded on the frontend.
        /// e.g. "Coworking Seat: Hourly Pass", "Executive Meeting Room: Half-Day (5 hrs)"
        /// Must exactly match one of the published rate sheet values.
        /// </summary>
        public string ResourcePackage { get; set; } = string.Empty;

        /// <summary>Desired booking date. Format: YYYY-MM-DD.</summary>
        public DateOnly Date { get; set; }

        /// <summary>Booking start time (local). Format: HH:mm.</summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>
        /// Booking end time (local). Required for Hourly packages — used to compute hours billed.
        /// Ignored for flat-rate packages (Day Pass, Half-Day, Full Day, Weekly, Monthly).
        /// </summary>
        public TimeOnly? EndTime { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>E.g. "+63"</summary>
        public string PhoneCountryCode { get; set; } = string.Empty;

        /// <summary>E.g. "9123456789"</summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Whether the guest is a student. Affects pricing for eligible packages:
        /// Coworking Seat Hourly → ₱40/hr (instead of ₱50/hr)
        /// Coworking Seat Day Pass / Weekly Pass / Monthly Desk → 10% discount
        /// Meeting and Collaboration rooms → no student discount.
        /// </summary>
        public bool IsStudent { get; set; }
    }
}
