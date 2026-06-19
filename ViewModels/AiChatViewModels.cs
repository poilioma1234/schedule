using System.ComponentModel.DataAnnotations;
using schedule.Models;

namespace schedule.ViewModels
{
    public class AiChatPageViewModel
    {
        public bool Embed { get; set; }

        public int? ConversationId { get; set; }

        public string? Prompt { get; set; }

        public List<AiChatConversationViewModel> Conversations { get; set; } = new();

        public List<AiChatMessageViewModel> Messages { get; set; } = new();
    }

    public class AiChatConversationViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string LastMessage { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }

        public bool IsActive { get; set; }
    }

    public class AiChatMessageViewModel
    {
        public int Id { get; set; }

        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public AiSchedulePlanViewModel? Plan { get; set; }
    }

    public class AiSchedulePlanViewModel
    {
        public List<AiScheduleSuggestionViewModel> Schedules { get; set; } = new();
    }

    public class AiScheduleSuggestionViewModel
    {
        public bool Include { get; set; } = true;

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        public bool IsImportant { get; set; }

        [Range(0, 10080)]
        public int ReminderMinutes { get; set; } = 30;

        public List<AiTaskSuggestionViewModel> Tasks { get; set; } = new();
    }

    public class AiTaskSuggestionViewModel
    {
        public bool Include { get; set; } = true;

        [Required]
        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(700)]
        public string? Description { get; set; }

        [Required]
        public DateTime Deadline { get; set; }

        public TaskPriorityLevel Priority { get; set; } = TaskPriorityLevel.Medium;
    }

    public class ApplyAiPlanViewModel
    {
        public bool Embed { get; set; }

        public int? ConversationId { get; set; }

        public int MessageId { get; set; }

        public List<AiScheduleSuggestionViewModel> Schedules { get; set; } = new();
    }

    public class AiTaskContextViewModel
    {
        public string Title { get; set; } = string.Empty;

        public DateTime Deadline { get; set; }

        public TaskPriorityLevel Priority { get; set; }

        public string? ScheduleTitle { get; set; }
    }

    public class AiScheduleContextViewModel
    {
        public string Title { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }
    }
}
