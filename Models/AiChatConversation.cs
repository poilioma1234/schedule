using System.ComponentModel.DataAnnotations;

namespace schedule.Models
{
    public class AiChatConversation
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(256)]
        public string? UserEmail { get; set; }

        [Required]
        [StringLength(160)]
        public string Title { get; set; } = "Chat mới";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
    }
}
