namespace Application.Common.Settings
{
    public class CoworkingSettings
    {
        public int AutoForfeitMinutes { get; set; } = 30;
        public int SessionEndingReminderMinutes { get; set; } = 15;
        public string ClientBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// One or more admin emails that receive booking and application notifications.
        /// Add as many as needed in appsettings.json under CoworkingSettings:AdminNotificationEmails.
        /// </summary>
        public List<string> AdminNotificationEmails { get; set; } = [];
    }
}
