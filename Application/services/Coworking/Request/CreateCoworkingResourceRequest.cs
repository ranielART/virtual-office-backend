using Domain.Enum;

namespace Application.Services.Coworking.Request
{
    public class CreateCoworkingResourceRequest
    {
        public string Name { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public int Capacity { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }
    }
}
