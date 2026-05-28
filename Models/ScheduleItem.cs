using System.ComponentModel.DataAnnotations;

namespace schedule.Models
{
    public class ScheduleItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Thời gian bắt đầu")]
        public DateTime StartTime { get; set; } = DateTime.Today.AddHours(8);

        [Required]
        [Display(Name = "Thời gian kết thúc")]
        public DateTime EndTime { get; set; } = DateTime.Today.AddHours(9);

        [StringLength(200)]
        public string? Location { get; set; }

        [Display(Name = "Lịch quan trọng")]
        public bool IsImportant { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(256)]
        public string? ReceiverEmail { get; set; }

        [Range(0, 10080)]
        public int ReminderMinutes { get; set; } = 5;

        public DateTime? ReminderSentAt { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByEmail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
