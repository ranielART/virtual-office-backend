using Domain.Enum;

namespace Application.Services.Public.Response
{
    public class AvailablePackageResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public PackageType PackageType { get; set; }
        public decimal PriceAmount { get; set; }
        public int? DurationHours { get; set; }
        public int? DurationDays { get; set; }
        public int? MaxCapacity { get; set; }
        public bool IsStudentEligible { get; set; }
        public decimal StudentDiscountPercent { get; set; }
        public decimal DownpaymentPercent { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
