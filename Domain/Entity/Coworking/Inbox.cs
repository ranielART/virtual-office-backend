using Domain.Common;

namespace Domain.Entity.Coworking
{
    public class Inbox : GuidBaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Phone { get; set; }

        public bool IsRead { get; set; } = false;
        public string Status { get; set; } = "New";

        public string? AdminNotes { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

    }
}