namespace Application.Services.Public.Request
{
    /// <summary>
    /// Multipart/form-data request for a public virtual office application.
    /// Business documents and down-payment screenshots are read separately from
    /// Request.Form.Files in the controller and passed into the command directly.
    /// </summary>
    public class SubmitVirtualOfficeApplicationRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>E.g. "+63"</summary>
        public string PhoneCountryCode { get; set; } = string.Empty;

        /// <summary>E.g. "9123456789"</summary>
        public string PhoneNumber { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public string BusinessType { get; set; } = string.Empty;

        /// <summary>True if applicant needs a registered business address for permits.</summary>
        public bool NeedsBusinessAddress { get; set; }

        /// <summary>True if applicant needs occasional access to physical coworking space.</summary>
        public bool NeedsOccasionalSpace { get; set; }

        /// <summary>Desired virtual office subscription duration in months. Minimum: 1.</summary>
        public int DurationMonths { get; set; }

        public string ServicePackage { get; set; } = string.Empty;
    }
}
