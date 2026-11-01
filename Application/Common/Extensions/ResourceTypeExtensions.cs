using Domain.Enum;

namespace Application.Common.Extensions
{
    public static class ResourceTypeExtensions
    {
        private static readonly Dictionary<string, ResourceType> StringToEnumMap = new()
        {
            { "coworking_seats", ResourceType.CoworkingSeats },
            { "executive_room", ResourceType.ExecutiveRoom },
            { "collaboration_room", ResourceType.CollaborationRoom },
            { "virtual_office", ResourceType.VirtualOffice },
            // Also allow the actual enum names
            { "CoworkingSeats", ResourceType.CoworkingSeats },
            { "ExecutiveRoom", ResourceType.ExecutiveRoom },
            { "CollaborationRoom", ResourceType.CollaborationRoom },
            { "VirtualOffice", ResourceType.VirtualOffice }
        };

        public static ResourceType? ParseResourceType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Try direct parsing first (for numeric values like "1", "2", "3")
            if (Enum.TryParse<ResourceType>(value, true, out var enumValue) && Enum.IsDefined(typeof(ResourceType), enumValue))
                return enumValue;

            // Try our custom mapping
            return StringToEnumMap.TryGetValue(value, out var mappedValue) ? mappedValue : null;
        }
    }
}