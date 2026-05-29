namespace schedule.ViewModels
{
    public class AdminUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string Roles { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsLocked { get; set; }
        public int ScheduleCount { get; set; }
        public int TodayScheduleCount { get; set; }
        public int ActiveOrUpcomingScheduleCount { get; set; }
        public int TotalTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public DateTime? LastScheduleAt { get; set; }
    }
}
