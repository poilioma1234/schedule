using schedule.ViewModels;

namespace schedule.Services
{
    public interface IAiChatService
    {
        Task<AiSchedulePlanResponse> GeneratePlanAsync(AiChatRequestContext context, CancellationToken cancellationToken = default);
    }

    public class AiChatRequestContext
    {
        public string Prompt { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        public DateTime Now { get; set; } = DateTime.Now;

        public List<AiTaskContextViewModel> OverdueTasks { get; set; } = new();

        public List<AiScheduleContextViewModel> UpcomingSchedules { get; set; } = new();

        public bool IsAdmin { get; set; }

        public string? SystemSummaryPrompt { get; set; }
    }

    public class AiSchedulePlanResponse
    {
        public string Reply { get; set; } = string.Empty;

        public AiSchedulePlanViewModel Plan { get; set; } = new();
    }
}
