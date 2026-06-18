using schedule.Models;

namespace schedule.ViewModels
{
    public class AdminDashboardViewModel
    {
        public string ActiveSection { get; set; } = "overview";
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
        public int AdminUsers { get; set; }
        public int TotalSchedules { get; set; }
        public int TodaySchedules { get; set; }
        public int ActiveOrUpcomingSchedules { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TodayTasks { get; set; }
        public string TodayTasksChange { get; set; } = string.Empty;
        public string OverdueTasksChange { get; set; } = string.Empty;
        public string CompletedRateChange { get; set; } = string.Empty;
        public string CreatedTasks7DaysChange { get; set; } = string.Empty;
        public string CompletedTasks7DaysChange { get; set; } = string.Empty;
        public string OverdueTasks7DaysChange { get; set; } = string.Empty;
        public bool EmailReminderEnabled { get; set; }
        public string SearchString { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = "all";
        public List<AdminUserViewModel> Users { get; set; } = new();
        public List<AdminActivityPointViewModel> ActivityPoints { get; set; } = new();
        public List<AdminUpcomingScheduleViewModel> UpcomingSchedules { get; set; } = new();
        public List<AdminOverdueTaskViewModel> OverdueTaskItems { get; set; } = new();
        public List<AdminScheduleRowViewModel> Schedules { get; set; } = new();
        public List<AdminTaskRowViewModel> Tasks { get; set; } = new();
        public List<AdminActivityEventViewModel> RecentActivities { get; set; } = new();
        public List<AdminNotificationViewModel> Notifications { get; set; } = new();
        public List<UserReport> PendingReports { get; set; } = new();
    }

    public class AdminActivityPointViewModel
    {
        public DateTime Date { get; set; }
        public string Label { get; set; } = string.Empty;
        public int ScheduleCount { get; set; }
        public int CreatedTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
    }

    public class AdminUpcomingScheduleViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? OwnerEmail { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsToday { get; set; }
    }

    public class AdminOverdueTaskViewModel
    {
        public int Id { get; set; }
        public int ScheduleItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? OwnerEmail { get; set; }
        public DateTime Deadline { get; set; }
        public int DaysOverdue { get; set; }
        public string AttentionText { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
    }

    public class AdminScheduleRowViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerUserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TaskCount { get; set; }
        public bool IsImportant { get; set; }
    }

    public class AdminTaskRowViewModel
    {
        public int Id { get; set; }
        public int ScheduleItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ScheduleTitle { get; set; }
        public string? OwnerEmail { get; set; }
        public DateTime Deadline { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string PriorityLabel { get; set; } = string.Empty;
        public string Color { get; set; } = "#0d6efd";
        public bool IsOverdue { get; set; }
    }

    public class AdminActivityEventViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string TimeElapsed { get; set; } = string.Empty;
        public string Tone { get; set; } = "blue";
    }

    public class AdminNotificationViewModel
    {
        public string Severity { get; set; } = "info";
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string? ActionLabel { get; set; }
    }
}
