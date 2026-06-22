using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Services
{
    public class GeminiAiChatService : IAiChatService
    {
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

            var isOpenRouter = !string.IsNullOrWhiteSpace(_settings.Endpoint) && _settings.Endpoint.Contains("openrouter.ai");
            HttpRequestMessage requestMessage;

            if (isOpenRouter)
            {
                var request = BuildOpenRouterRequest(context);
                requestMessage = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
                {
                    Content = JsonContent.Create(request)
                };
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
                requestMessage.Headers.Add("HTTP-Referer", "https://github.com/schedule-manager");
                requestMessage.Headers.Add("X-Title", "Schedule Manager");
            }
            else
            {
                var endpointTemplate = string.IsNullOrWhiteSpace(_settings.Endpoint)
                    ? "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}"
                    : _settings.Endpoint;

                var url = string.Format(
                    endpointTemplate,
                    Uri.EscapeDataString(_settings.Model),
                    Uri.EscapeDataString(_settings.ApiKey));

                var request = BuildGeminiRequest(context);
                requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(request)
                };
            }

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
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
            _logger.LogInformation("Raw AI Text parsed: {Text}", text);
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
                            
                            BẢO MẬT & VAI TRÒ:
                            - Nếu người dùng đăng nhập là User bình thường, bạn chỉ có quyền truy cập và phân tích dữ liệu cá nhân của chính user đó (trong phần Task quá hạn và Lịch sắp tới của họ) và thông tin Bảng xếp hạng / Chuỗi Streak công khai của các người dùng trong hệ thống (được cung cấp ở phần thông tin công khai). Tuyệt đối không được biết, không được phân tích, và không được bịa ra thông tin riêng tư (như danh sách task riêng tư) của bất kỳ người dùng khác.
                            - Chỉ khi người dùng đăng nhập là ADMIN (được đánh dấu trong context) thì bạn mới được cung cấp và phân tích dữ liệu thống kê chi tiết, lịch sử quá hạn riêng tư của các user khác.
                            
                            GIAO TIẾP & TRẢ LỜI:
                            - Nếu Admin/User chào hỏi thông thường (ví dụ: "chào", "chào bạn", "hello", "hi", "xin chào"), bạn CHỈ cần chào lại một cách lịch sự, ngắn gọn và hỏi xem có thể giúp gì cho họ. Tuyệt đối KHÔNG tự động liệt kê bất kỳ số liệu thống kê hoặc danh sách task quá hạn nào khi chào hỏi.
                            - Khi Admin hỏi xem các task quá hạn của các user trong hôm qua/nay/ngày nào đó trong quá khứ:
                              + CHỈ đưa ra tên hiển thị (hoặc email) của user đó và số lượng task quá hạn tương ứng của họ trên ngày đó. Bạn có thể liệt kê thêm tiêu đề các task quá hạn của họ nếu cần thiết.
                              + Tuyệt đối KHÔNG hiển thị các số liệu thống kê chung không liên quan của toàn hệ thống (như tổng số task hệ thống, tỷ lệ phần trạng thái quá hạn toàn hệ thống).
                              + Tuyệt đối KHÔNG tự động đưa ra các quy trình đề xuất, lời khuyên xử lý, bài học, hay định hướng quy trình trừ khi Admin yêu cầu tư vấn.
                              + Trả lời cực kỳ ngắn gọn, trực diện, đúng trọng tâm câu hỏi.
                            - Khi User hỏi về Bảng xếp hạng hoặc Chuỗi Streak:
                              + Sử dụng dữ liệu Bảng xếp hạng / Chuỗi Streak công khai được cung cấp để trả lời chính xác, trực tiếp câu hỏi (ví dụ: "Ai có chuỗi cao nhất?", "Hạng của tôi/ai đó là bao nhiêu?").
                              + Trả lời ngắn gọn, thân thiện.
                            
                            LẬP LỊCH:
                            - Nếu người dùng (đặc biệt là Admin) chỉ hỏi để xem thông tin, phân tích dữ liệu, báo cáo hoặc thống kê mà không yêu cầu tạo lịch mới, bạn hãy trả về danh sách lịch (schedules) là rỗng, và tập trung viết câu trả lời phân tích chi tiết, đầy đủ thông tin vào phần reply.
                            - Nếu tạo lịch, hãy trả về các mốc thời gian hợp lý trong tương lai theo giờ địa phương.
                            - Chỉ tạo tối đa 10 lịch, mỗi lịch tối đa 5 task để tiết kiệm token.
                            - Không hứa rằng lịch đã được lưu; người dùng phải bấm Áp dụng vào calendar.
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

        private object BuildOpenRouterRequest(AiChatRequestContext context)
        {
            var systemInstructionText = """
                Bạn là trợ lý AI cho ứng dụng Schedule Manager.
                Nhiệm vụ: gợi ý lịch học/công việc, chia nhỏ thành task, đề xuất deadline và ưu tiên.
                Luôn trả lời tiếng Việt, ngắn gọn, thực dụng.
                
                BẢO MẬT & VAI TRÒ:
                - Nếu người dùng đăng nhập là User bình thường, bạn chỉ có quyền truy cập và phân tích dữ liệu cá nhân của chính user đó (trong phần Task quá hạn và Lịch sắp tới của họ) và thông tin Bảng xếp hạng / Chuỗi Streak công khai của các người dùng trong hệ thống (được cung cấp ở phần thông tin công khai). Tuyệt đối không được biết, không được phân tích, và không được bịa ra thông tin riêng tư (như danh sách task riêng tư) của bất kỳ người dùng khác.
                - Chỉ khi người dùng đăng nhập là ADMIN (được đánh dấu trong context) thì bạn mới được cung cấp và phân tích dữ liệu thống kê chi tiết, lịch sử quá hạn riêng tư của các user khác.
                
                GIAO TIẾP & TRẢ LỜI:
                - Nếu Admin/User chào hỏi thông thường (ví dụ: "chào", "chào bạn", "hello", "hi", "xin chào"), bạn CHỈ cần chào lại một cách lịch sự, ngắn gọn và hỏi xem có thể giúp gì cho họ. Tuyệt đối KHÔNG tự động liệt kê bất kỳ số liệu thống kê hoặc danh sách task quá hạn nào khi chào hỏi.
                - Khi Admin hỏi xem các task quá hạn của các user trong hôm qua/nay/ngày nào đó trong quá khứ:
                  + CHỈ đưa ra tên hiển thị (hoặc email) của user đó và số lượng task quá hạn tương ứng của họ trên ngày đó. Bạn có thể liệt kê thêm tiêu đề các task quá hạn của họ nếu cần thiết.
                  + Tuyệt đối KHÔNG hiển thị các số liệu thống kê chung không liên quan của toàn hệ thống (như tổng số task hệ thống, tỷ lệ phần trạng thái quá hạn toàn hệ thống).
                  + Tuyệt đối KHÔNG tự động đưa ra các quy trình đề xuất, lời khuyên xử lý, bài học, hay định hướng quy trình trừ khi Admin yêu cầu tư vấn.
                  + Trả lời cực kỳ ngắn gọn, trực diện, đúng trọng tâm câu hỏi.
                - Khi User hỏi về Bảng xếp hạng hoặc Chuỗi Streak:
                  + Sử dụng dữ liệu Bảng xếp hạng / Chuỗi Streak công khai được cung cấp để trả lời chính xác, trực tiếp câu hỏi (ví dụ: "Ai có chuỗi cao nhất?", "Hạng của tôi/ai đó là bao nhiêu?").
                  + Trả lời ngắn gọn, thân thiện.
                
                LẬP LỊCH:
                - Nếu người dùng (đặc biệt là Admin) chỉ hỏi để xem thông tin, phân tích dữ liệu, báo cáo hoặc thống kê mà không yêu cầu tạo lịch mới, bạn hãy trả về danh sách lịch (schedules) là rỗng, và tập trung viết câu trả lời phân tích chi tiết, đầy đủ thông tin vào phần reply.
                - Nếu tạo lịch, hãy trả về các mốc thời gian hợp lý trong tương lai theo giờ địa phương.
                - Chỉ tạo tối đa 10 lịch, mỗi lịch tối đa 5 task để tiết kiệm token.
                - Không hứa rằng lịch đã được lưu; người dùng phải bấm Áp dụng vào calendar.
                """;

            return new
            {
                model = _settings.Model,
                messages = new[]
                {
                    new { role = "system", content = systemInstructionText },
                    new { role = "user", content = BuildPrompt(context) }
                },
                temperature = _settings.Temperature,
                max_tokens = _settings.MaxOutputTokens,
                response_format = new { type = "json_object" }
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

            var userContext = "";
            if (context.IsAdmin && !string.IsNullOrWhiteSpace(context.SystemSummaryPrompt))
            {
                userContext = $"\n\nBẠN ĐANG TRÒ CHUYỆN VỚI QUẢN TRỊ VIÊN (ADMIN). Dữ liệu hoạt động toàn hệ thống của tất cả các user:\n{context.SystemSummaryPrompt}\nHãy sử dụng các số liệu trên để trả lời, phân tích, lập biểu đồ văn bản hoặc đánh giá hiệu suất khi Admin yêu cầu.";
            }
            else if (!context.IsAdmin && !string.IsNullOrWhiteSpace(context.SystemSummaryPrompt))
            {
                userContext = $"\n\nTHÔNG TIN CÔNG KHAI HỆ THỐNG (Bảng xếp hạng và chuỗi streak của các user công khai):\n{context.SystemSummaryPrompt}\nBạn có thể sử dụng dữ liệu này để trả lời các câu hỏi của người dùng về xếp hạng, so sánh hoặc ai đang giữ chuỗi streak cao nhất hiện tại.";
            }

            return $"""
                   Hôm nay: {context.Now:yyyy-MM-dd HH:mm}
                   User: {context.UserEmail} {userContext}

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
            var root = document.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? "{}";
                }
            }

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array &&
                    parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? "{}";
                }
            }

            return "{}";
        }

        private static AiSchedulePlanResponse DeserializePlan(string text)
        {
            var payload = ExtractJsonObject(text);
            GeminiPlanPayload? raw = null;
            try
            {
                raw = JsonSerializer.Deserialize<GeminiPlanPayload>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                var reply = ExtractReplyRegex(text);
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    raw = new GeminiPlanPayload { Reply = reply, Schedules = new() };
                }
            }

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

        private static string ExtractReplyRegex(string text)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, @"""reply""\s*:\s*""((?:[^""\\]|\\.)*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r");
                }
            }
            catch
            {
                // Ignore regex errors
            }
            return "";
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
            var start = ParseLocalDateTime(schedule.ActualStartTime, DateTime.Now.AddHours(1));
            var end = ParseLocalDateTime(schedule.ActualEndTime, start.AddHours(1));

            if (end <= start)
            {
                end = start.AddHours(1);
            }

            return new AiScheduleSuggestionViewModel
            {
                Include = true,
                Title = TrimTo(schedule.ActualTitle, 120) ?? "Lịch AI đề xuất",
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
                Title = TrimTo(task.ActualTitle, 160) ?? "Task AI đề xuất",
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
            public string? Name { get; set; }

            public string? ActualTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Name;

            public string? Description { get; set; }

            public string? StartTime { get; set; }
            public string? Start { get; set; }

            public string? ActualStartTime => !string.IsNullOrWhiteSpace(StartTime) ? StartTime : Start;

            public string? EndTime { get; set; }
            public string? End { get; set; }

            public string? ActualEndTime => !string.IsNullOrWhiteSpace(EndTime) ? EndTime : End;

            public string? Location { get; set; }

            public bool IsImportant { get; set; }

            public int ReminderMinutes { get; set; }

            public List<GeminiTaskPayload>? Tasks { get; set; }
        }

        private sealed class GeminiTaskPayload
        {
            public string? Title { get; set; }
            public string? Name { get; set; }

            public string? ActualTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Name;

            public string? Description { get; set; }

            public string? Deadline { get; set; }

            public string? Priority { get; set; }
        }
    }
}
