namespace Application.Common.Settings
{
    public class CoworkingSettings
    {
        public int AutoForfeitMinutes { get; set; } = 30;
        public int SessionEndingReminderMinutes { get; set; } = 15;

        // Admin email that receives booking notifications
        public string AdminNotificationEmail { get; set; } = "ranielmontebon24@gmail.com";
    }
}
