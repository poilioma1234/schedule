namespace schedule.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
        public int AdminUsers { get; set; }
        public int TotalSchedules { get; set; }
        public int TodaySchedules { get; set; }
        public int ActiveOrUpcomingSchedules { get; set; }
        public string SearchString { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = "all";
        public List<AdminUserViewModel> Users { get; set; } = new();
    }
}
