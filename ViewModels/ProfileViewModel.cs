using schedule.Models;

namespace schedule.ViewModels
{
    public class ProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public bool IsPublicProfile { get; set; }
        public string PublicProfilePath { get; set; } = string.Empty;
        public bool ShouldAutoplayMusic { get; set; }
        public string? YouTubeEmbedUrl { get; set; }
        public UserProfile Profile { get; set; } = new();
        public int TotalSchedules { get; set; }
        public int TodaySchedules { get; set; }
        public int ImportantSchedules { get; set; }
        public int ActiveOrUpcomingSchedules { get; set; }
        public int CompletedTaskCount { get; set; }
        public string RankLabel { get; set; } = "Chưa có dữ liệu xếp hạng";
    }
}
