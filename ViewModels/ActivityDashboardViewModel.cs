namespace schedule.ViewModels
{
    public class ActivityDashboardViewModel
    {
        public string UserEmail { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int PendingTasks { get; set; }
        public int ImportantSchedules { get; set; }
        public int CurrentStreakDays { get; set; }
        public int BestStreakDays { get; set; }
        public ActivityChartViewModel DailyTasks { get; set; } = new();
        public ActivityChartViewModel WeeklyTasks { get; set; } = new();
        public ActivityChartViewModel MonthlyTasks { get; set; } = new();
        public ActivityChartViewModel YearlyTasks { get; set; } = new();
        public ActivityChartViewModel CompletedTaskChart { get; set; } = new();
        public ActivityChartViewModel OverdueTaskChart { get; set; } = new();
        public ActivityChartViewModel ImportantScheduleChart { get; set; } = new();
        public List<ActivityEventViewModel> RecentActivities { get; set; } = new();
        public List<ActivityReminderViewModel> Reminders { get; set; } = new();
    }

    public class ActivityEventViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string Tone { get; set; } = "blue";
    }

    public class ActivityReminderViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
        public string Tone { get; set; } = "blue";
    }
}
