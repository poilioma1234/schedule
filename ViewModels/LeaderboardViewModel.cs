using schedule.Models;

namespace schedule.ViewModels
{
    public class LeaderboardViewModel
    {
        public string Period { get; set; } = "month";
        public string PeriodTitle { get; set; } = string.Empty;
        public string SelectedMonth { get; set; } = string.Empty;
        public string SelectedDate { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsFinalizedPeriod { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public List<LeaderboardRowViewModel> Rows { get; set; } = new();
        public List<LeaderboardAward> MonthlyAwards { get; set; } = new();
    }
}
