using System.ComponentModel.DataAnnotations;

namespace schedule.ViewModels
{
    public class EditProfileViewModel
    {
        [StringLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(600)]
        public string? Bio { get; set; }

        [Url(ErrorMessage = "Link nhạc không hợp lệ.")]
        [StringLength(500)]
        public string? MusicUrl { get; set; }

        [Url(ErrorMessage = "Link Facebook không hợp lệ.")]
        [StringLength(500)]
        public string? FacebookUrl { get; set; }

        [Url(ErrorMessage = "Link YouTube không hợp lệ.")]
        [StringLength(500)]
        public string? YouTubeUrl { get; set; }

        [Url(ErrorMessage = "Link TikTok không hợp lệ.")]
        [StringLength(500)]
        public string? TikTokUrl { get; set; }

        [Url(ErrorMessage = "Website không hợp lệ.")]
        [StringLength(500)]
        public string? WebsiteUrl { get; set; }

        public string? CurrentAvatarPath { get; set; }
        public string? CurrentCoverPath { get; set; }
        public IFormFile? AvatarFile { get; set; }
        public IFormFile? CoverFile { get; set; }
    }
}
