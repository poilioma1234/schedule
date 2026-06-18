namespace schedule.Services
{
    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string PublicSlug { get; set; } = string.Empty;
        public int CompletedTaskCount { get; set; }
        public int OnTimeTaskCount { get; set; }
        public int UrgentTaskCount { get; set; }
        public int Score { get; set; }

        public int OnTimeRate => CompletedTaskCount == 0
            ? 0
            : (int)Math.Round(OnTimeTaskCount * 100.0 / CompletedTaskCount);
    }
}
