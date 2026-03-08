using Domain.Enum;

namespace Application.Services.Coworking.Response
{
    public class CoworkingResourceResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public int Capacity { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
