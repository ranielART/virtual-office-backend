using Domain.Enum;

namespace Application.Services.Coworking.Request
{
    public class WalkInTimeInRequest
    {
        public Guid ResourceId { get; set; }
        public Guid ServicePackageId { get; set; }

        /// <summary>
        /// Required only for Hourly packages. Ignored for all other package types —
        /// ScheduledEnd is derived from the package's DurationHours / DurationDays.
        /// </summary>
        public decimal? EstimatedHours { get; set; }

        public string GuestFirstName { get; set; } = string.Empty;
        public string GuestLastName { get; set; } = string.Empty;
        public string GuestPhone { get; set; } = string.Empty;
        public string? GuestEmail { get; set; }
        public bool IsStudent { get; set; }

        /// <summary>
        /// Must be one of the schemes the selected package allows
        /// (AllowsUpfront / AllowsPostpaid / AllowsDaily).
        /// </summary>
        public PaymentScheme PaymentScheme { get; set; } = PaymentScheme.Postpaid;
    }
}
