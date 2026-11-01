namespace Application.Services.Coworking.Response
{
    public class CheckWalkInAvailabilityResponse
    {
        public Guid ResourceId { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public DateTime? NextAvailableAt { get; set; }
    }
}
