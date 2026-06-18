namespace schedule.ViewModels
{
    public class SettingsViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsProfilePublic { get; set; }
        public bool EmailReminderEnabled { get; set; }
        public string PublicProfilePath { get; set; } = string.Empty;
    }
}
