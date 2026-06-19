using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Services
{
    public class GeminiAiChatService : IAiChatService
    {
        private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiAiChatService> _logger;
        private readonly GeminiAiSettings _settings;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public GeminiAiChatService(
            HttpClient httpClient,
            IOptions<GeminiAiSettings> settings,
            ILogger<GeminiAiChatService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<AiSchedulePlanResponse> GeneratePlanAsync(
            AiChatRequestContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                return new AiSchedulePlanResponse
                {
                    Reply = "AI chưa được cấu hình API key. Hãy làm theo Docs/AI_CHATBOT_SETUP.md, sau đó hỏi lại để AI tạo lịch và task.",
                    Plan = new AiSchedulePlanViewModel()
                };
            }

            var request = BuildGeminiRequest(context);
            var url = string.Format(
                Endpoint,
                Uri.EscapeDataString(_settings.Model),
                Uri.EscapeDataString(_settings.ApiKey));

            using var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini API returned {StatusCode}: {Body}",
                    (int)response.StatusCode,
                    responseBody);

                return new AiSchedulePlanResponse
                {
                    Reply = "AI đang trả lỗi từ Gemini API. Kiểm tra API key, quota, model và thử lại sau.",
                    Plan = new AiSchedulePlanViewModel()
                };
            }

            var text = ExtractCandidateText(responseBody);
            var result = DeserializePlan(text);

            NormalizePlan(result.Plan);
            return result;
        }

        private object BuildGeminiRequest(AiChatRequestContext context)
        {
            return new
            {
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = """
                            Bạn là trợ lý AI cho ứng dụng Schedule Manager.
                            Nhiệm vụ: gợi ý lịch học/công việc, chia nhỏ thành task, đề xuất deadline và ưu tiên.
                            Luôn trả lời tiếng Việt, ngắn gọn, thực dụng.
                            Nếu người dùng hỏi phân tích task quá hạn, hãy nêu cách sắp xếp lại theo deadline và mức ưu tiên.
                            Nếu tạo lịch, hãy trả về các mốc thời gian hợp lý trong tương lai theo giờ địa phương.
                            Chỉ tạo tối đa 10 lịch, mỗi lịch tối đa 5 task để tiết kiệm token.
                            Không hứa rằng lịch đã được lưu; người dùng phải bấm Áp dụng vào calendar.
                            """
                        }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = BuildPrompt(context) }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = _settings.Temperature,
                    maxOutputTokens = _settings.MaxOutputTokens,
                    responseMimeType = "application/json",
                    responseSchema = BuildResponseSchema()
                }
            };
        }

        private static string BuildPrompt(AiChatRequestContext context)
        {
            var overdue = context.OverdueTasks.Any()
                ? string.Join("\n", context.OverdueTasks.Select(task =>
                    $"- {task.Title}; deadline {task.Deadline:yyyy-MM-dd HH:mm}; ưu tiên {task.Priority}; lịch: {task.ScheduleTitle}"))
                : "- Không có task quá hạn.";

            var upcoming = context.UpcomingSchedules.Any()
                ? string.Join("\n", context.UpcomingSchedules.Select(schedule =>
                    $"- {schedule.Title}; {schedule.StartTime:yyyy-MM-dd HH:mm} - {schedule.EndTime:yyyy-MM-dd HH:mm}"))
                : "- Chưa có lịch sắp tới.";

            return $"""
                   Hôm nay: {context.Now:yyyy-MM-dd HH:mm}
                   User: {context.UserEmail}

                   Yêu cầu của user:
                   {context.Prompt}

                   Task quá hạn hiện tại:
                   {overdue}

                   Lịch sắp tới hiện tại:
                   {upcoming}

                   Trả về JSON đúng schema gồm:
                   - reply: phần trả lời hiển thị trong chat.
                   - schedules: danh sách lịch đề xuất để user có thể chỉnh sửa và áp dụng.
                   Nếu không cần tạo lịch, schedules để mảng rỗng.
                   """;
        }

        private static object BuildResponseSchema()
        {
            return new
            {
                type = "OBJECT",
                properties = new Dictionary<string, object>
                {
                    ["reply"] = new { type = "STRING" },
                    ["schedules"] = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new Dictionary<string, object>
                            {
                                ["title"] = new { type = "STRING" },
                                ["description"] = new { type = "STRING" },
                                ["startTime"] = new { type = "STRING" },
                                ["endTime"] = new { type = "STRING" },
                                ["location"] = new { type = "STRING" },
                                ["isImportant"] = new { type = "BOOLEAN" },
                                ["reminderMinutes"] = new { type = "INTEGER" },
                                ["tasks"] = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        properties = new Dictionary<string, object>
                                        {
                                            ["title"] = new { type = "STRING" },
                                            ["description"] = new { type = "STRING" },
                                            ["deadline"] = new { type = "STRING" },
                                            ["priority"] = new { type = "STRING" }
                                        },
                                        required = new[] { "title", "deadline", "priority" }
                                    }
                                }
                            },
                            required = new[] { "title", "startTime", "endTime", "tasks" }
                        }
                    }
                },
                required = new[] { "reply", "schedules" }
            };
        }

        private static string ExtractCandidateText(string responseBody)
        {
            using var document = JsonDocument.Parse(responseBody);
            var candidates = document.RootElement.GetProperty("candidates");

            if (candidates.GetArrayLength() == 0)
            {
                return "{}";
            }

            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            return parts.GetArrayLength() == 0
                ? "{}"
                : parts[0].GetProperty("text").GetString() ?? "{}";
        }

        private static AiSchedulePlanResponse DeserializePlan(string text)
        {
            var payload = ExtractJsonObject(text);
            var raw = JsonSerializer.Deserialize<GeminiPlanPayload>(payload, JsonOptions);

            var plan = new AiSchedulePlanViewModel
            {
                Schedules = raw?.Schedules?
                    .Select(MapSchedule)
                    .Where(schedule => !string.IsNullOrWhiteSpace(schedule.Title))
                    .ToList() ?? new List<AiScheduleSuggestionViewModel>()
            };

            return new AiSchedulePlanResponse
            {
                Reply = string.IsNullOrWhiteSpace(raw?.Reply)
                    ? "AI đã tạo gợi ý. Bạn có thể chỉnh sửa rồi áp dụng vào calendar."
                    : raw.Reply.Trim(),
                Plan = plan
            };
        }

        private static string ExtractJsonObject(string text)
        {
            var trimmed = text.Trim();
            var first = trimmed.IndexOf('{');
            var last = trimmed.LastIndexOf('}');

            return first >= 0 && last > first
                ? trimmed[first..(last + 1)]
                : "{\"reply\":\"AI trả về dữ liệu không đúng định dạng.\",\"schedules\":[]}";
        }

        private static AiScheduleSuggestionViewModel MapSchedule(GeminiSchedulePayload schedule)
        {
            var start = ParseLocalDateTime(schedule.StartTime, DateTime.Now.AddHours(1));
            var end = ParseLocalDateTime(schedule.EndTime, start.AddHours(1));

            if (end <= start)
            {
                end = start.AddHours(1);
            }

            return new AiScheduleSuggestionViewModel
            {
                Include = true,
                Title = TrimTo(schedule.Title, 120) ?? "Lịch AI đề xuất",
                Description = TrimTo(schedule.Description, 500),
                StartTime = start,
                EndTime = end,
                Location = TrimTo(schedule.Location, 200),
                IsImportant = schedule.IsImportant,
                ReminderMinutes = Math.Clamp(schedule.ReminderMinutes <= 0 ? 30 : schedule.ReminderMinutes, 0, 10080),
                Tasks = schedule.Tasks?
                    .Select(task => MapTask(task, start))
                    .Where(task => !string.IsNullOrWhiteSpace(task.Title))
                    .ToList() ?? new List<AiTaskSuggestionViewModel>()
            };
        }

        private static AiTaskSuggestionViewModel MapTask(GeminiTaskPayload task, DateTime fallbackDeadline)
        {
            return new AiTaskSuggestionViewModel
            {
                Include = true,
                Title = TrimTo(task.Title, 160) ?? "Task AI đề xuất",
                Description = TrimTo(task.Description, 700),
                Deadline = ParseLocalDateTime(task.Deadline, fallbackDeadline),
                Priority = ParsePriority(task.Priority)
            };
        }

        private static DateTime ParseLocalDateTime(string? value, DateTime fallback)
        {
            return DateTime.TryParse(value, out var parsed)
                ? parsed
                : fallback;
        }

        private static TaskPriorityLevel ParsePriority(string? value)
        {
            return Enum.TryParse<TaskPriorityLevel>(value, true, out var priority)
                ? priority
                : TaskPriorityLevel.Medium;
        }

        private static void NormalizePlan(AiSchedulePlanViewModel plan)
        {
            plan.Schedules = plan.Schedules
                .Take(10)
                .ToList();

            foreach (var schedule in plan.Schedules)
            {
                schedule.Tasks = schedule.Tasks.Take(5).ToList();
            }
        }

        private static string? TrimTo(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private sealed class GeminiPlanPayload
        {
            public string? Reply { get; set; }

            public List<GeminiSchedulePayload>? Schedules { get; set; }
        }

        private sealed class GeminiSchedulePayload
        {
            public string? Title { get; set; }

            public string? Description { get; set; }

            public string? StartTime { get; set; }

            public string? EndTime { get; set; }

            public string? Location { get; set; }

            public bool IsImportant { get; set; }

            public int ReminderMinutes { get; set; }

            public List<GeminiTaskPayload>? Tasks { get; set; }
        }

        private sealed class GeminiTaskPayload
        {
            public string? Title { get; set; }

            public string? Description { get; set; }

            public string? Deadline { get; set; }

            public string? Priority { get; set; }
        }
    }
}
