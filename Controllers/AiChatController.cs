using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Helpers;
using schedule.Models;
using schedule.Services;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize]
    public class AiChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AiChatController> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public AiChatController(
            IAiChatService aiChatService,
            ApplicationDbContext context,
            ILogger<AiChatController> logger,
            UserManager<IdentityUser> userManager)
        {
            _aiChatService = aiChatService;
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(bool embed = false, int? conversationId = null, bool newChat = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            return View(await BuildPageModelAsync(user.Id, embed, conversationId, newChat));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(AiChatPageViewModel model, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var prompt = model.Prompt?.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                TempData["AiChatError"] = "Vui lòng nhập yêu cầu cho AI.";
                return RedirectToChat(model.Embed, model.ConversationId);
            }

            var conversation = await GetOrCreateConversationAsync(user, prompt, model.ConversationId, cancellationToken);

            var userMessage = new AiChatMessage
            {
                ConversationId = conversation.Id,
                UserId = user.Id,
                UserEmail = user.Email,
                Role = "user",
                Content = TrimTo(prompt, 4000)
            };

            _context.AiChatMessages.Add(userMessage);
            await _context.SaveChangesAsync(cancellationToken);

            AiSchedulePlanResponse aiResult;
            try
            {
                aiResult = await _aiChatService.GeneratePlanAsync(
                    await BuildAiContextAsync(user, prompt, cancellationToken),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI chat request failed.");
                aiResult = new AiSchedulePlanResponse
                {
                    Reply = "AI đang gặp lỗi khi xử lý yêu cầu. Kiểm tra cấu hình Gemini API hoặc thử lại sau.",
                    Plan = new AiSchedulePlanViewModel()
                };
            }

            var assistantMessage = new AiChatMessage
            {
                ConversationId = conversation.Id,
                UserId = user.Id,
                UserEmail = user.Email,
                Role = "assistant",
                Content = TrimTo(aiResult.Reply, 4000),
                PlanJson = aiResult.Plan.Schedules.Any()
                    ? JsonSerializer.Serialize(aiResult.Plan)
                    : null
            };

            _context.AiChatMessages.Add(assistantMessage);
            conversation.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return RedirectToChat(model.Embed, conversation.Id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(ApplyAiPlanViewModel model, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var sourceMessage = await _context.AiChatMessages
                .FirstOrDefaultAsync(message =>
                    message.Id == model.MessageId
                    && message.UserId == user.Id
                    && message.Role == "assistant",
                    cancellationToken);

            if (sourceMessage == null)
            {
                return NotFound();
            }

            var selectedSchedules = model.Schedules
                .Where(schedule => schedule.Include)
                .Take(10)
                .ToList();

            var createdCount = 0;
            var skippedCount = 0;

            foreach (var suggestion in selectedSchedules)
            {
                if (string.IsNullOrWhiteSpace(suggestion.Title) || suggestion.EndTime <= suggestion.StartTime)
                {
                    skippedCount++;
                    continue;
                }

                var schedule = new ScheduleItem
                {
                    Title = TrimTo(suggestion.Title, 120),
                    Description = TrimNullableTo(suggestion.Description, 500),
                    StartTime = suggestion.StartTime,
                    EndTime = suggestion.EndTime,
                    Location = TrimNullableTo(suggestion.Location, 200),
                    IsImportant = suggestion.IsImportant,
                    ReceiverEmail = user.Email,
                    ReminderMinutes = Math.Clamp(suggestion.ReminderMinutes, 0, 10080),
                    CreatedByUserId = user.Id,
                    CreatedByEmail = user.Email ?? User.Identity?.Name,
                    CreatedAt = DateTime.Now
                };

                _context.ScheduleItems.Add(schedule);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var taskSuggestion in suggestion.Tasks.Where(task => task.Include).Take(5))
                {
                    if (string.IsNullOrWhiteSpace(taskSuggestion.Title))
                    {
                        continue;
                    }

                    var priority = taskSuggestion.Priority;
                    _context.TaskItems.Add(new TaskItem
                    {
                        ScheduleItemId = schedule.Id,
                        Title = TrimTo(taskSuggestion.Title, 160),
                        Description = TrimNullableTo(taskSuggestion.Description, 700),
                        Deadline = taskSuggestion.Deadline,
                        Priority = priority,
                        Status = taskSuggestion.Deadline < DateTime.Now
                            ? TaskItemStatus.Overdue
                            : TaskItemStatus.NotStarted,
                        Color = TaskDisplayHelper.PriorityColor(priority),
                        CreatedByUserId = user.Id,
                        CreatedByEmail = user.Email ?? User.Identity?.Name,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }

                createdCount++;
            }

            if (createdCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _context.AiChatMessages.Add(new AiChatMessage
                {
                    ConversationId = sourceMessage.ConversationId,
                    UserId = user.Id,
                    UserEmail = user.Email,
                    Role = "assistant",
                    Content = skippedCount > 0
                        ? $"Đã áp dụng {createdCount} lịch vào calendar. Bỏ qua {skippedCount} lịch do thiếu tiêu đề hoặc thời gian không hợp lệ."
                        : $"Đã áp dụng {createdCount} lịch vào calendar."
                });

                if (sourceMessage.ConversationId.HasValue)
                {
                    var conversation = await _context.AiChatConversations
                        .FirstOrDefaultAsync(item => item.Id == sourceMessage.ConversationId.Value && item.UserId == user.Id, cancellationToken);
                    if (conversation != null)
                    {
                        conversation.UpdatedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                TempData["SuccessMessage"] = $"Đã áp dụng {createdCount} lịch AI vào calendar.";
                if (model.Embed)
                {
                    return RedirectToChat(true, sourceMessage.ConversationId);
                }

                return RedirectToAction("Index", "Schedule");
            }

            TempData["AiChatError"] = "Chưa có lịch hợp lệ để áp dụng. Kiểm tra tiêu đề và thời gian kết thúc.";
            return RedirectToChat(model.Embed, model.ConversationId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear(bool embed = false, int? conversationId = null, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            if (conversationId.HasValue)
            {
                var conversation = await _context.AiChatConversations
                    .FirstOrDefaultAsync(item => item.Id == conversationId.Value && item.UserId == userId, cancellationToken);

                if (conversation != null)
                {
                    var messages = await _context.AiChatMessages
                        .Where(message => message.ConversationId == conversation.Id && message.UserId == userId)
                        .ToListAsync(cancellationToken);

                    _context.AiChatMessages.RemoveRange(messages);
                    _context.AiChatConversations.Remove(conversation);
                    await _context.SaveChangesAsync(cancellationToken);
                    TempData["SuccessMessage"] = "Đã xóa đoạn chat AI.";
                }

                return RedirectToAction(nameof(Index), new { embed });
            }

            var allMessages = await _context.AiChatMessages
                .Where(message => message.UserId == userId)
                .ToListAsync(cancellationToken);
            var conversations = await _context.AiChatConversations
                .Where(conversation => conversation.UserId == userId)
                .ToListAsync(cancellationToken);

            _context.AiChatMessages.RemoveRange(allMessages);
            _context.AiChatConversations.RemoveRange(conversations);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Đã xóa lịch sử AI Chat của bạn.";
            return RedirectToAction(nameof(Index), new { embed });
        }

        private IActionResult RedirectToChat(bool embed, int? conversationId = null)
        {
            return embed
                ? RedirectToAction(nameof(Index), new { embed = true, conversationId })
                : RedirectToAction(nameof(Index), new { conversationId });
        }

        private async Task<AiChatPageViewModel> BuildPageModelAsync(
            string userId,
            bool embed,
            int? conversationId,
            bool newChat)
        {
            var conversations = await _context.AiChatConversations
                .Where(conversation => conversation.UserId == userId)
                .OrderByDescending(conversation => conversation.UpdatedAt)
                .Take(30)
                .ToListAsync();

            var activeConversation = newChat
                ? null
                : conversations.FirstOrDefault(conversation => conversation.Id == conversationId)
                    ?? conversations.FirstOrDefault();

            var messages = await _context.AiChatMessages
                .Where(message =>
                    message.UserId == userId
                    && activeConversation != null
                    && message.ConversationId == activeConversation.Id)
                .OrderByDescending(message => message.CreatedAt)
                .Take(30)
                .OrderBy(message => message.CreatedAt)
                .ToListAsync();

            return new AiChatPageViewModel
            {
                Embed = embed,
                ConversationId = activeConversation?.Id,
                Conversations = await BuildConversationListAsync(conversations, activeConversation?.Id),
                Messages = messages.Select(MapMessage).ToList()
            };
        }

        private async Task<List<AiChatConversationViewModel>> BuildConversationListAsync(
            List<AiChatConversation> conversations,
            int? activeConversationId)
        {
            var result = new List<AiChatConversationViewModel>();
            foreach (var conversation in conversations)
            {
                var lastMessage = await _context.AiChatMessages
                    .Where(message => message.ConversationId == conversation.Id)
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.Content)
                    .FirstOrDefaultAsync();

                result.Add(new AiChatConversationViewModel
                {
                    Id = conversation.Id,
                    Title = conversation.Title,
                    LastMessage = TrimNullableTo(lastMessage, 90) ?? "Chưa có tin nhắn",
                    UpdatedAt = conversation.UpdatedAt,
                    IsActive = conversation.Id == activeConversationId
                });
            }

            return result;
        }

        private async Task<AiChatConversation> GetOrCreateConversationAsync(
            IdentityUser user,
            string prompt,
            int? conversationId,
            CancellationToken cancellationToken)
        {
            AiChatConversation? conversation = null;
            if (conversationId.HasValue)
            {
                conversation = await _context.AiChatConversations
                    .FirstOrDefaultAsync(item => item.Id == conversationId.Value && item.UserId == user.Id, cancellationToken);
            }

            if (conversation != null)
            {
                conversation.UserEmail = user.Email;
                conversation.UpdatedAt = DateTime.Now;
                return conversation;
            }

            conversation = new AiChatConversation
            {
                UserId = user.Id,
                UserEmail = user.Email,
                Title = BuildConversationTitle(prompt),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.AiChatConversations.Add(conversation);
            await _context.SaveChangesAsync(cancellationToken);
            return conversation;
        }

        private static string BuildConversationTitle(string prompt)
        {
            var compact = string.Join(" ", prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(compact))
            {
                return "Chat mới";
            }

            return compact.Length <= 60 ? compact : $"{compact[..60]}...";
        }

        private AiChatMessageViewModel MapMessage(AiChatMessage message)
        {
            return new AiChatMessageViewModel
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                CreatedAt = message.CreatedAt,
                Plan = DeserializePlan(message.PlanJson)
            };
        }

        private static AiSchedulePlanViewModel? DeserializePlan(string? planJson)
        {
            if (string.IsNullOrWhiteSpace(planJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<AiSchedulePlanViewModel>(planJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task<AiChatRequestContext> BuildAiContextAsync(
            IdentityUser user,
            string prompt,
            CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var overdueTasks = await _context.TaskItems
                .Include(task => task.ScheduleItem)
                .Where(task =>
                    task.CreatedByUserId == user.Id
                    && task.Status != TaskItemStatus.Completed
                    && task.Deadline < now)
                .OrderBy(task => task.Deadline)
                .Take(8)
                .Select(task => new AiTaskContextViewModel
                {
                    Title = task.Title,
                    Deadline = task.Deadline,
                    Priority = task.Priority,
                    ScheduleTitle = task.ScheduleItem != null ? task.ScheduleItem.Title : null
                })
                .ToListAsync(cancellationToken);

            var upcomingSchedules = await _context.ScheduleItems
                .Where(schedule => schedule.CreatedByUserId == user.Id && schedule.EndTime >= now)
                .OrderBy(schedule => schedule.StartTime)
                .Take(8)
                .Select(schedule => new AiScheduleContextViewModel
                {
                    Title = schedule.Title,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime
                })
                .ToListAsync(cancellationToken);

            return new AiChatRequestContext
            {
                Prompt = prompt,
                UserEmail = user.Email ?? User.Identity?.Name ?? "user",
                Now = now,
                OverdueTasks = overdueTasks,
                UpcomingSchedules = upcomingSchedules
            };
        }

        private static string TrimTo(string value, int maxLength)
        {
            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static string? TrimNullableTo(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
