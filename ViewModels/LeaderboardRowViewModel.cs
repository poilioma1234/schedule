namespace schedule.ViewModels
{
    public class LeaderboardRowViewModel
    {
        public int Rank { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string PublicProfilePath { get; set; } = string.Empty;
        public int CompletedTaskCount { get; set; }
        public int Score { get; set; }
        public int UrgentTaskCount { get; set; }
    }
}
