using schedule.Models;

namespace schedule.ViewModels
{
    public class HomeDashboardViewModel
    {
        public bool IsAuthenticated { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public int TotalSchedules { get; set; }
        public int TodaySchedules { get; set; }
        public int ActiveSchedules { get; set; }
        public int UpcomingSchedules { get; set; }
        public int ImportantSchedules { get; set; }
        public int TodayTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }
        public int PendingTaskCount { get; set; }
        public List<ScheduleItem> UpcomingItems { get; set; } = new();
        public List<TaskItem> TodayTasks { get; set; } = new();
        public List<TaskItem> OverdueTasks { get; set; } = new();
        public List<HomeActivityItemViewModel> RecentActivities { get; set; } = new();
        public List<ScheduleItem> Reminders { get; set; } = new();
    }

    public class HomeActivityItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string Tone { get; set; } = "blue";
    }
}
