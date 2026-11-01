using Domain.Enum;

namespace Application.Common.Extensions
{
    public static class BusinessTypeExtensions
    {
        private static readonly Dictionary<string, BusinessType> StringToEnumMap = new()
        {
            { "sole_proprietorship", BusinessType.SoleProprietorship },
            { "partnership", BusinessType.Partnership },
            { "corporation", BusinessType.Corporation },
            { "SoleProprietorship", BusinessType.SoleProprietorship },
            { "Partnership", BusinessType.Partnership },
            { "Corporation", BusinessType.Corporation }
        };

        public static BusinessType? ParseBusinessType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (Enum.TryParse<BusinessType>(value, true, out var enumValue) && Enum.IsDefined(typeof(BusinessType), enumValue))
                return enumValue;

            return StringToEnumMap.TryGetValue(value, out var mappedValue) ? mappedValue : null;
        }
    }
}
