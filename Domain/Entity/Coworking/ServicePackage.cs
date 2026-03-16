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

        // Payment scheme options — at least one must be true.
        // FrontDesk picks one of the enabled schemes at time-in.
        // AllowsUpfront is invalid for Hourly (total unknown at time-in).
        // AllowsDaily is invalid for non-Monthly packages.
        public bool AllowsUpfront  { get; set; } = false;
        public bool AllowsPostpaid { get; set; } = true;
        public bool AllowsDaily    { get; set; } = false;

        public bool IsActive { get; set; } = true;
    }
}
