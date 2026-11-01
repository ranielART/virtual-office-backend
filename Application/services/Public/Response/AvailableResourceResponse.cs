using Domain.Enum;

namespace Application.Services.Public.Response
{
    public class AvailableResourceResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public int Capacity { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
