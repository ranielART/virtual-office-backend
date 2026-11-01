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

        /// <summary>
        /// Absolute URL to the brand logo used in email templates.
        /// Example: https://your-api.com/images/communside-logo.png
        /// </summary>
        public string LogoUrl { get; set; } = string.Empty;
    }
}
