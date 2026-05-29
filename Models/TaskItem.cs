using System.ComponentModel.DataAnnotations;

namespace schedule.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        [Required]
        public int ScheduleItemId { get; set; }

        public ScheduleItem? ScheduleItem { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề task.")]
        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(700)]
        public string? Description { get; set; }

        [Required]
        public DateTime Deadline { get; set; } = DateTime.Today.AddHours(17);

        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;

        public TaskPriorityLevel Priority { get; set; } = TaskPriorityLevel.Medium;

        [StringLength(24)]
        public string Color { get; set; } = "#0d6efd";

        [Url(ErrorMessage = "Link đính kèm không hợp lệ.")]
        [StringLength(700)]
        public string? AttachmentUrl { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByEmail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
