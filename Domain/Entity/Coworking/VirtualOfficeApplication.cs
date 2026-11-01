using Domain.Common;
using Domain.Enum;

namespace Domain.Entity.Coworking
{
    public class VirtualOfficeApplication : GuidBaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public BusinessType BusinessType { get; set; }
        public bool NeedsBusinessAddress { get; set; }
        public bool NeedsOccasionalSpace { get; set; }
        public int DurationMonths { get; set; }
        public string? ServicePackage { get; set; }
        // public Guid? ServicePackageId { get; set; }
        // public ServicePackage? ServicePackageEntity { get; set; }
        public VirtualOfficeApplicationStatus Status { get; set; } = VirtualOfficeApplicationStatus.Pending;

        // JSON arrays of Azure Blob absolute URLs
        public List<string> BusinessDocumentUrls { get; set; } = [];
        public List<string> DownPaymentProofUrls { get; set; } = [];

        public string? AdminNotes { get; set; }

        // String FK to AspNetUsers
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
