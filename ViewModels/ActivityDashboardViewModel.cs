namespace schedule.ViewModels
{
    public class ActivityDashboardViewModel
    {
        public string UserEmail { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
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
    }
}
