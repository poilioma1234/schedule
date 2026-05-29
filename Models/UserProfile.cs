using System.ComponentModel.DataAnnotations;

namespace schedule.Models
{
    public class UserProfile
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(90)]
        public string? PublicSlug { get; set; }

        public bool IsProfilePublic { get; set; } = true;

        [StringLength(600)]
        public string? Bio { get; set; }

        [StringLength(300)]
        public string? AvatarPath { get; set; }

        [StringLength(300)]
        public string? CoverPath { get; set; }

        [StringLength(500)]
        public string? MusicUrl { get; set; }

        [StringLength(500)]
        public string? FacebookUrl { get; set; }

        [StringLength(500)]
        public string? YouTubeUrl { get; set; }

        [StringLength(500)]
        public string? TikTokUrl { get; set; }

        [StringLength(500)]
        public string? WebsiteUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
