using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.DTOs;
using schedule.Helpers;
using schedule.Models;
using schedule.Services;
using schedule.ViewModels;

namespace schedule.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/ai")]
    public class AiChatApiController : ControllerBase
    {
        private readonly IAiChatService _aiChatService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILeaderboardService _leaderboardService;

        public AiChatApiController(
            IAiChatService aiChatService,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILeaderboardService leaderboardService)
        {
            _aiChatService = aiChatService;
            _context = context;
            _userManager = userManager;
            _leaderboardService = leaderboardService;
        }

        [HttpGet("conversations")]
        [ProducesResponseType(typeof(IEnumerable<AiChatConversationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConversations()
        {
            var userId = _userManager.GetUserId(User);
            var conversations = await _context.AiChatConversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new AiChatConversationDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return Ok(conversations);
        }

        [HttpGet("conversations/{id:int}/messages")]
        [ProducesResponseType(typeof(IEnumerable<AiChatMessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConversationMessages(int id)
        {
            var userId = _userManager.GetUserId(User);
            var conversation = await _context.AiChatConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (conversation == null)
            {
                return NotFound("Cuộc hội thoại không tồn tại.");
            }

            var messages = await _context.AiChatMessages
                .Where(m => m.ConversationId == id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new AiChatMessageDto
                {
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(messages);
        }

        public class ChatRequestModel
        {
            public string Prompt { get; set; } = string.Empty;
            public int? ConversationId { get; set; }
        }

        [HttpPost("chat")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Chat([FromBody] ChatRequestModel request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest("Prompt không được để trống.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var prompt = request.Prompt.Trim();
            var conversation = await GetOrCreateConversationAsync(user, prompt, request.ConversationId, cancellationToken);

            var userMessage = new AiChatMessage
            {
                ConversationId = conversation.Id,
                UserId = user.Id,
                UserEmail = user.Email,
                Role = "user",
                Content = prompt.Length > 4000 ? prompt[..4000] : prompt
            };

            _context.AiChatMessages.Add(userMessage);
            await _context.SaveChangesAsync(cancellationToken);

            AiSchedulePlanResponse aiResult;
            try
            {
                var aiContext = await BuildAiContextAsync(user, prompt, cancellationToken);
                aiResult = await _aiChatService.GeneratePlanAsync(aiContext, cancellationToken);
            }
            catch (Exception)
            {
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
                Content = aiResult.Reply,
                PlanJson = aiResult.Plan.Schedules.Any() ? JsonSerializer.Serialize(aiResult.Plan) : null
            };

            _context.AiChatMessages.Add(assistantMessage);
            conversation.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                ConversationId = conversation.Id,
                Reply = aiResult.Reply,
                Plan = aiResult.Plan.Schedules.Any() ? aiResult.Plan : null
            });
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

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            return new AiChatRequestContext
            {
                Now = now,
                UserEmail = user.Email ?? "Không rõ email",
                Prompt = prompt,
                OverdueTasks = overdueTasks,
                UpcomingSchedules = upcomingSchedules,
                IsAdmin = isAdmin,
                SystemSummaryPrompt = publicRankingsSummary
            };
        }
    }
}
