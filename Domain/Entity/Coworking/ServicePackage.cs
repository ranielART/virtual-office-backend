using Domain.Common;
using Domain.Enum;

namespace Domain.Entity.Coworking
{
    public class ServicePackage : GuidBaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public PackageType PackageType { get; set; }
        public decimal PriceAmount { get; set; }

        // Duration — null means metered (Hourly)
        public int? DurationHours { get; set; }
        public int? DurationDays { get; set; }

        public int? MaxCapacity { get; set; }
        public bool IsStudentEligible { get; set; }
        public decimal StudentDiscountPercent { get; set; }

        // Downpayment percent for online bookings (e.g. 50.00 = 50%)
        public decimal DownpaymentPercent { get; set; } = 50;

        public bool IsActive { get; set; } = true;
    }
}
