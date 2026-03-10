namespace Application.Services.Coworking.Request
{
    public class WalkInTimeOutRequest
    {
        /// <summary>
        /// Override the actual checkout time. Use when FrontDesk forgot to process time-out
        /// and the guest had already left earlier. Must be in the past and after ActualStart.
        /// When omitted, DateTime.UtcNow is used.
        /// </summary>
        public DateTime? ActualEndOverride { get; set; }

        /// <summary>
        /// Optional notes explaining a late or corrected time-out (e.g., "Guest left at 11:00 AM,
        /// time-out processed late due to staff oversight").
        /// </summary>
        public string? TimeOutNotes { get; set; }
    }
}
