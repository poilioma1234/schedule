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
        public List<ScheduleItem> UpcomingItems { get; set; } = new();
    }
}
