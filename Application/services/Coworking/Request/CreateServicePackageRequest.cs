using Domain.Enum;

namespace Application.Services.Coworking.Request
{
    public class CreateServicePackageRequest
    {
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public PackageType PackageType { get; set; }
        public decimal PriceAmount { get; set; }
        public int? DurationHours { get; set; }
        public int? DurationDays { get; set; }
        public int? MaxCapacity { get; set; }
        public bool IsStudentEligible { get; set; }
        public decimal StudentDiscountPercent { get; set; }
        public decimal DownpaymentPercent { get; set; } = 50;
        public bool AllowsUpfront  { get; set; } = false;
        public bool AllowsPostpaid { get; set; } = true;
        public bool AllowsDaily    { get; set; } = false;
    }
}
