using System.ComponentModel.DataAnnotations;

namespace schedule.Models
{
    public class AiChatMessage
    {
        public int Id { get; set; }

        public int? ConversationId { get; set; }

        public AiChatConversation? Conversation { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(256)]
        public string? UserEmail { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "user";

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = string.Empty;

        public string? PlanJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
