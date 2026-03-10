using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class AvailabilityResponse
    {
        public Guid ResourceId { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }
    }
}
