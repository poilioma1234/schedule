using System;

namespace schedule.Models
{
    public class SystemOverviewStats
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
        public int AdminUsers { get; set; }
        public int RegularUsers { get; set; }
        public int PublicProfiles { get; set; }
        public int PendingReports { get; set; }

        public int TotalSchedules { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int OverdueTasks { get; set; }
    }

    public class UserReportRow
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public int ScheduleCount { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
