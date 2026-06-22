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
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AiChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AiChatController> _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILeaderboardService _leaderboardService;

        public AiChatController(
            IAiChatService aiChatService,
            ApplicationDbContext context,
            ILogger<AiChatController> logger,
            UserManager<IdentityUser> userManager,
            ILeaderboardService leaderboardService)
        {
            _aiChatService = aiChatService;
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _leaderboardService = leaderboardService;
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
            var today = now.Date;

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

            // Fetch public rankings and streaks for all users (both Admin and regular User can ask about this)
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var leaderboardEntries = await _leaderboardService.BuildRowsAsync(monthStart, monthEnd);

            var publicProfiles = await _context.UserProfiles
                .AsNoTracking()
                .Where(p => p.IsProfilePublic)
                .ToListAsync(cancellationToken);

            var publicUserIds = publicProfiles.Select(p => p.UserId).ToList();
            var allPublicTasks = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.CreatedByUserId != null && publicUserIds.Contains(t.CreatedByUserId))
                .ToListAsync(cancellationToken);

            var userStreaks = publicProfiles.Select(p =>
            {
                var userTasks = allPublicTasks.Where(t => t.CreatedByUserId == p.UserId);
                var streak = ActivityStatsHelper.CalculateCompletionStreak(userTasks, today);
                return new
                {
                    p.DisplayName,
                    p.UserId,
                    CurrentStreak = streak.Current,
                    LongestStreak = streak.Longest
                };
            }).OrderByDescending(x => x.CurrentStreak).ToList();

            var leaderboardRows = leaderboardEntries.Select(r => $"| #{r.Rank} | {r.DisplayName} ({r.Email}) | {r.Score} điểm | {r.CompletedTaskCount} task |").ToList();
            var streakRows = userStreaks.Select((s, idx) => $"| #{idx + 1} | {s.DisplayName} | {s.CurrentStreak} ngày | {s.LongestStreak} ngày |").ToList();

            var publicRankingsSummary = $"""
            ### BẢNG XẾP HẠNG THÁNG NÀY (Tháng {monthStart:MM/yyyy}):
            | Hạng | Người dùng | Điểm số | Số task hoàn thành |
            |---|---|---|---|
            {(leaderboardRows.Any() ? string.Join("\n", leaderboardRows) : "| (Chưa có dữ liệu) | | | |")}

            ### BẢNG XẾP HẠNG CHUỖI STREAK CÔNG KHAI (Hiện tại):
            | Hạng | Tên người dùng | Chuỗi hiện tại | Chuỗi kỷ lục |
            |---|---|---|---|
            {(streakRows.Any() ? string.Join("\n", streakRows) : "| (Chưa có dữ liệu) | | | |")}
            """;

            string? systemSummary = null;
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin)
            {
                // Query system-wide overview
                var totalUsers = await _context.Users.CountAsync(cancellationToken);
                var pendingReports = await _context.UserReports.CountAsync(r => r.Status == ReportStatus.Pending, cancellationToken);
                var totalSchedules = await _context.ScheduleItems.CountAsync(cancellationToken);
                var totalTasks = await _context.TaskItems.CountAsync(cancellationToken);
                var completedTasks = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Completed, cancellationToken);
                var overdueTasksCount = await _context.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline < now, cancellationToken);

                // Query details for each user
                var usersList = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
                var profileMap = await _context.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, p => p, cancellationToken);
                var adminRoleId = await _context.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);
                var adminUserIds = adminRoleId != null 
                    ? await _context.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync(cancellationToken)
                    : new List<string>();

                var userDetailsList = new List<string>();
                foreach (var u in usersList)
                {
                    var isUAdmin = adminUserIds.Contains(u.Id);
                    var displayName = profileMap.TryGetValue(u.Id, out var profile) ? profile.DisplayName : (u.Email ?? u.UserName ?? "");
                    var isLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
                    
                    var uSchedules = await _context.ScheduleItems.CountAsync(s => s.CreatedByUserId == u.Id, cancellationToken);
                    var uTasks = await _context.TaskItems.CountAsync(t => t.CreatedByUserId == u.Id, cancellationToken);
                    var uCompleted = await _context.TaskItems.CountAsync(t => t.CreatedByUserId == u.Id && t.Status == TaskItemStatus.Completed, cancellationToken);
                    var uOverdue = await _context.TaskItems.CountAsync(t => t.CreatedByUserId == u.Id && t.Status != TaskItemStatus.Completed && t.Deadline < now, cancellationToken);

                    userDetailsList.Add($"| {u.Email} | {displayName} | {(isUAdmin ? "Admin" : "User")} | {(isLocked ? "Bị khóa" : "Hoạt động")} | {uSchedules} | {uTasks} | {uCompleted} | {uOverdue} |");
                }

                var tomorrow = today.AddDays(1);
                var tasksDueToday = await _context.TaskItems
                    .Include(t => t.ScheduleItem)
                    .Where(t => t.Status != TaskItemStatus.Completed && t.Deadline >= today && t.Deadline < tomorrow)
                    .OrderBy(t => t.Deadline)
                    .Select(t => new {
                        t.Title,
                        t.Deadline,
                        t.Priority,
                        t.CreatedByEmail,
                        ScheduleTitle = t.ScheduleItem != null ? t.ScheduleItem.Title : null
                    })
                    .ToListAsync(cancellationToken);

                var tasksDueTodayDetailsList = new List<string>();
                foreach (var t in tasksDueToday)
                {
                    var isPassed = t.Deadline < now;
                    var statusStr = isPassed ? "Đã quá hạn" : "Chưa quá hạn";
                    tasksDueTodayDetailsList.Add($"- Task: \"{t.Title}\" | Hạn chót: {t.Deadline:HH:mm} ({statusStr}) | Ưu tiên: {t.Priority} | Tạo bởi: {t.CreatedByEmail} | Lịch trình: {t.ScheduleTitle}");
                }

                var tasksDueTodaySection = tasksDueTodayDetailsList.Any()
                    ? string.Join("\n", tasksDueTodayDetailsList)
                    : "- Không có task nào có hạn chót trong ngày hôm nay.";

                var allOverdueTasks = await _context.TaskItems
                    .Include(t => t.ScheduleItem)
                    .Where(t => t.Status != TaskItemStatus.Completed && t.Deadline < now)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.Deadline)
                    .Take(100)
                    .Select(t => new {
                        t.Title,
                        t.Deadline,
                        t.Priority,
                        t.CreatedByEmail,
                        ScheduleTitle = t.ScheduleItem != null ? t.ScheduleItem.Title : null
                    })
                    .ToListAsync(cancellationToken);

                var allOverdueDetailsList = new List<string>();
                foreach (var t in allOverdueTasks)
                {
                    allOverdueDetailsList.Add($"- Task: \"{t.Title}\" | Hạn chót: {t.Deadline:yyyy-MM-dd HH:mm} | Ưu tiên: {t.Priority} | Tạo bởi: {t.CreatedByEmail} | Lịch trình: {t.ScheduleTitle}");
                }

                var allOverdueSection = allOverdueDetailsList.Any()
                    ? string.Join("\n", allOverdueDetailsList)
                    : "- Không có task nào đang quá hạn trên hệ thống.";

                // Compile history of overdue task counts and titles per day/user for the last 7 days
                var allTasksInDb = await _context.TaskItems
                    .AsNoTracking()
                    .Select(t => new { t.Title, t.CreatedByEmail, t.Deadline, t.Status, t.UpdatedAt })
                    .ToListAsync(cancellationToken);

                var userEmails = usersList.Select(u => u.Email).ToList();
                var overdueByDayAndUser = new Dictionary<string, Dictionary<string, List<string>>>();

                for (int i = 0; i < 8; i++)
                {
                    var targetDate = now.Date.AddDays(-i);
                    var dateStr = targetDate.ToString("yyyy-MM-dd");
                    var limit = targetDate.AddDays(1);
                    
                    var userTasksMap = allTasksInDb
                        .Where(t => t.Deadline >= targetDate && t.Deadline < limit && !string.IsNullOrEmpty(t.CreatedByEmail))
                        .Where(t => t.Status != TaskItemStatus.Completed || t.UpdatedAt >= limit)
                        .GroupBy(t => t.CreatedByEmail!)
                        .ToDictionary(g => g.Key, g => g.Select(t => t.Title).ToList());
                        
                    overdueByDayAndUser[dateStr] = userTasksMap;
                }

                var overdueHistoryRows = new List<string>();
                foreach (var kvp in overdueByDayAndUser)
                {
                    var dateStr = kvp.Key;
                    foreach (var userEmail in userEmails)
                    {
                        if (string.IsNullOrEmpty(userEmail)) continue;
                        if (kvp.Value.TryGetValue(userEmail, out var taskTitles) && taskTitles.Any())
                        {
                            var count = taskTitles.Count;
                            var titlesStr = string.Join("; ", taskTitles);
                            overdueHistoryRows.Add($"| {dateStr} | {userEmail} | {count} | {titlesStr} |");
                        }
                    }
                }
                var overdueHistorySection = overdueHistoryRows.Any()
                    ? string.Join("\n", overdueHistoryRows)
                    : "| (Không có) | | | |";

                systemSummary = $"""
                {publicRankingsSummary}

                ### THÔNG TIN HỆ THỐNG DÀNH RIÊNG CHO ADMIN (TUYỆT ĐỐI BẢO MẬT):
                - Tổng số người dùng: {totalUsers}
                - Tổng số lịch trình: {totalSchedules}
                - Tổng số nhiệm vụ (Tasks): {totalTasks} (Đã xong: {completedTasks}, Quá hạn: {overdueTasksCount})
                - Phản ánh từ user chưa xử lý: {pendingReports}

                ### Danh sách chi tiết người dùng:
                | Email | Tên hiển thị | Vai trò | Trạng thái | Lịch trình | Tasks | Đã xong | Quá hạn |
                |---|---|---|---|---|---|---|---|
                {string.Join("\n", userDetailsList)}

                ### Bảng thống kê số lượng và tên các task có HẠN CHÓT (Deadline) trong ngày đó nhưng bị quá hạn (chưa hoàn thành tính đến cuối ngày đó) của từng User (7 ngày qua):
                | Ngày (yyyy-MM-dd) | Email người dùng | Số lượng task quá hạn có hạn chót trong ngày | Danh sách tên task quá hạn |
                |---|---|---|---|
                {overdueHistorySection}

                ### Danh sách Task có hạn chót trong hôm nay (Chưa hoàn thành):
                {tasksDueTodaySection}

                ### Danh sách các Task đang quá hạn trên hệ thống (Tối đa 100 task):
                {allOverdueSection}
                """;
            }
            else
            {
                systemSummary = publicRankingsSummary;
            }

            return new AiChatRequestContext
            {
                Prompt = prompt,
                UserEmail = user.Email ?? User.Identity?.Name ?? "user",
                Now = now,
                OverdueTasks = overdueTasks,
                UpcomingSchedules = upcomingSchedules,
                IsAdmin = isAdmin,
                SystemSummaryPrompt = systemSummary
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
