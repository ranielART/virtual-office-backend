namespace Application.Services.Coworking.Request
{
    public class WalkInTimeInRequest
    {
        public Guid ResourceId { get; set; }
        public Guid ServicePackageId { get; set; }
        public decimal EstimatedHours { get; set; }
        public string GuestFirstName { get; set; } = string.Empty;
        public string GuestLastName { get; set; } = string.Empty;
        public string GuestPhone { get; set; } = string.Empty;
        public string? GuestEmail { get; set; }
        public bool IsStudent { get; set; }
    }
}
