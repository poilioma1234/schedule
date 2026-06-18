using System.ComponentModel.DataAnnotations;

namespace schedule.Models
{
    public class LeaderboardAward
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string UserEmailSnapshot { get; set; } = string.Empty;

        [Required]
        [StringLength(160)]
        public string DisplayNameSnapshot { get; set; } = string.Empty;

        [Required]
        [StringLength(24)]
        public string Period { get; set; } = "month";

        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        public int Rank { get; set; }

        public int Score { get; set; }

        public int CompletedTaskCount { get; set; }

        public int OnTimeTaskCount { get; set; }

        public DateTime AwardedAt { get; set; } = DateTime.Now;
    }
}
