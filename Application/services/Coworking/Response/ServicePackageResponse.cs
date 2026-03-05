using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class ServicePackageResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public string ResourceTypeName => ResourceType.ToString();
        public PackageType PackageType { get; set; }
        public string PackageTypeName => PackageType.ToString();
        public decimal PriceAmount { get; set; }
        public int? DurationHours { get; set; }
        public int? DurationDays { get; set; }
        public int? MaxCapacity { get; set; }
        public bool IsStudentEligible { get; set; }
        public decimal StudentDiscountPercent { get; set; }
        public decimal DownpaymentPercent { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
