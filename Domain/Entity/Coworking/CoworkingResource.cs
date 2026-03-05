using Domain.Common;
using Domain.Enum;

namespace Domain.Entity.Coworking
{
    public class CoworkingResource : GuidBaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public int Capacity { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        // Navigation
        public ICollection<Booking> Bookings { get; set; } = [];
        public ICollection<ServicePackage> ServicePackages { get; set; } = [];
    }
}
