namespace Application.Services.Coworking.Request
{
    public class UpdateCoworkingResourceRequest
    {
        public string? Name { get; set; }
        public int? Capacity { get; set; }
        public string? Description { get; set; }
        public int? SortOrder { get; set; }
    }
}
