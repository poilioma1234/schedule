using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace schedule.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/profile")]
    public class ProfileApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILeaderboardService _leaderboardService;
        private readonly IEmailService _emailService;

        public ProfileApiController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILeaderboardService leaderboardService,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _leaderboardService = leaderboardService;
            _emailService = emailService;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var profile = await GetOrCreateProfileAsync(user);
            var dto = await MapToProfileDtoAsync(user, profile, isPublicProfile: false);
            return Ok(dto);
        }

        [HttpPut("me")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfileUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var profile = await GetOrCreateProfileAsync(user);
            profile.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName)
                ? user.Email ?? user.UserName ?? "User"
                : dto.DisplayName.Trim();
            profile.Bio = dto.Bio?.Trim();
            profile.IsProfilePublic = dto.IsProfilePublic;
            profile.MusicUrl = dto.MusicUrl?.Trim();
            profile.FacebookUrl = dto.FacebookUrl?.Trim();
            profile.YouTubeUrl = dto.YouTubeUrl?.Trim();
            profile.TikTokUrl = dto.TikTokUrl?.Trim();
            profile.WebsiteUrl = dto.WebsiteUrl?.Trim();
            profile.UpdatedAt = DateTime.Now;

            if (string.IsNullOrWhiteSpace(profile.PublicSlug))
            {
                profile.PublicSlug = await CreateUniqueSlugAsync(user);
            }

            await _context.SaveChangesAsync();
            var updatedDto = await MapToProfileDtoAsync(user, profile, isPublicProfile: false);
            return Ok(updatedDto);
        }

        [AllowAnonymous]
        [HttpGet("user/{slug}")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPublicProfile(string slug)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(item => item.PublicSlug == slug);
            if (profile == null)
            {
                return NotFound("Hồ sơ không tồn tại.");
            }

            var user = await _userManager.FindByIdAsync(profile.UserId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var isOwner = User.Identity?.IsAuthenticated == true && _userManager.GetUserId(User) == user.Id;
            var canViewPrivateProfile = isOwner || User.IsInRole("Admin");
            if (!profile.IsProfilePublic && !canViewPrivateProfile)
            {
                return NotFound("Hồ sơ đang ở chế độ riêng tư.");
            }

            var dto = await MapToProfileDtoAsync(user, profile, isPublicProfile: true);
            return Ok(dto);
        }

        [HttpPost("report")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SubmitReport([FromBody] UserReportCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var reporterId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(dto.ReportedUserId) || string.IsNullOrWhiteSpace(dto.Reason))
            {
                return BadRequest("Thiếu thông tin báo cáo.");
            }

            if (reporterId == dto.ReportedUserId)
            {
                return BadRequest("Không thể tự báo cáo bản thân.");
            }

            var recentReport = await _context.UserReports
                .Where(r => r.ReporterUserId == reporterId && r.ReportedUserId == dto.ReportedUserId
                         && r.CreatedAt > DateTime.Now.AddHours(-24))
                .AnyAsync();

            if (recentReport)
            {
                return BadRequest("Bạn đã báo cáo người dùng này trong 24 giờ qua. Vui lòng chờ thêm.");
            }

            var report = new UserReport
            {
                ReportedUserId = dto.ReportedUserId,
                ReporterUserId = reporterId,
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "other" : dto.Category,
                Reason = dto.Reason.Trim()[..Math.Min(dto.Reason.Trim().Length, 1000)],
                CreatedAt = DateTime.Now,
                Status = ReportStatus.Pending
            };

            _context.UserReports.Add(report);
            await _context.SaveChangesAsync();

            // Notify admin via email
            try
            {
                var reportedUser = await _userManager.FindByIdAsync(dto.ReportedUserId);
                var reporterUser = await _userManager.FindByIdAsync(reporterId ?? "");
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                var adminEmail = adminUsers.FirstOrDefault()?.Email;

                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    var categoryLabel = dto.Category switch
                    {
                        "spam" => "Spam / Quảng cáo",
                        "harassment" => "Quấy rối / Ngôn từ không phù hợp",
                        "fake" => "Giả mạo danh tính",
                        "inappropriate" => "Nội dung không phù hợp",
                        _ => "Khác"
                    };

                    var adminLink = $"{Request.Scheme}://{Request.Host}/Admin?section=notifications";

                    await _emailService.SendEmailAsync(
                        adminEmail,
                        "[HUTECH Schedule] Có báo cáo người dùng mới",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#dc2626;'>&#9888; Báo cáo người dùng</h2>" +
                        $"<p><strong>Người bị báo cáo:</strong> {reportedUser?.Email ?? dto.ReportedUserId}</p>" +
                        $"<p><strong>Người báo cáo:</strong> {reporterUser?.Email ?? "Ẩn danh"}</p>" +
                        $"<p><strong>Danh mục:</strong> {categoryLabel}</p>" +
                        "<p><strong>Lý do:</strong></p>" +
                        $"<blockquote style='border-left:4px solid #dc2626;padding:12px;background:#fef2f2;border-radius:6px;'>{System.Net.WebUtility.HtmlEncode(dto.Reason)}</blockquote>" +
                        $"<p><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        $"<a href='{adminLink}' style='display:inline-block;padding:10px 20px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;margin-top:12px;'>Xem tại Admin Dashboard</a>" +
                        "</div>");
                }
            }
            catch { /* Do not break workflow if email fails */ }

            return Ok(new { success = true, message = "Báo cáo đã được gửi. Admin sẽ xem xét trong thời gian sớm nhất." });
        }

        private async Task<UserProfile> GetOrCreateProfileAsync(IdentityUser user)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id);
            if (profile != null)
            {
                if (string.IsNullOrWhiteSpace(profile.PublicSlug))
                {
                    profile.PublicSlug = await CreateUniqueSlugAsync(user);
                    await _context.SaveChangesAsync();
                }
                return profile;
            }

            profile = new UserProfile
            {
                UserId = user.Id,
                DisplayName = user.Email ?? user.UserName ?? "User",
                PublicSlug = await CreateUniqueSlugAsync(user),
                IsProfilePublic = true
            };

            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        private async Task<string> CreateUniqueSlugAsync(IdentityUser user)
        {
            var source = user.Email?.Split('@')[0] ?? user.UserName ?? "user";
            var baseSlug = Slugify(source);
            var candidate = baseSlug;
            var counter = 2;

            while (await _context.UserProfiles.AnyAsync(item => item.PublicSlug == candidate && item.UserId != user.Id))
            {
                candidate = $"{baseSlug}-{counter}";
                counter++;
            }

            return candidate;
        }

        private static string Slugify(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            var slug = Regex.Replace(builder.ToString().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "user" : slug[..Math.Min(slug.Length, 80)];
        }

        private async Task<UserProfileDto> MapToProfileDtoAsync(IdentityUser user, UserProfile profile, bool isPublicProfile)
        {
            var scheduleQuery = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);
            var taskQuery = _context.TaskItems.Where(item => item.CreatedByUserId == user.Id);
            var now = DateTime.Now;

            var tasks = await taskQuery.ToListAsync();
            var streak = ActivityStatsHelper.CalculateCompletionStreak(tasks, DateTime.Today);
            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            var rankLabel = await BuildCurrentRankLabelAsync(user.Id, profile.IsProfilePublic, isLocked);

            return new UserProfileDto
            {
                UserId = user.Id,
                DisplayName = profile.DisplayName,
                Bio = profile.Bio,
                AvatarPath = profile.AvatarPath,
                CoverPath = profile.CoverPath,
                PublicSlug = profile.PublicSlug,
                IsProfilePublic = profile.IsProfilePublic,
                MusicUrl = profile.MusicUrl,
                FacebookUrl = profile.FacebookUrl,
                YouTubeUrl = profile.YouTubeUrl,
                TikTokUrl = profile.TikTokUrl,
                WebsiteUrl = profile.WebsiteUrl,
                Email = isPublicProfile ? null : user.Email, // hide email on public queries
                TotalSchedules = await scheduleQuery.CountAsync(),
                TotalTasks = tasks.Count,
                CompletedTaskCount = tasks.Count(item => item.Status == TaskItemStatus.Completed),
                OverdueTaskCount = tasks.Count(item => item.Status != TaskItemStatus.Completed && item.Deadline < now),
                CurrentStreakDays = streak.Current,
                LongestStreakDays = streak.Longest,
                RankLabel = rankLabel
            };
        }

        private async Task<string> BuildCurrentRankLabelAsync(string userId, bool isProfilePublic, bool isLocked)
        {
            if (isLocked)
            {
                return "User bị khóa";
            }

            if (!isProfilePublic)
            {
                return "Không tham gia bảng xếp hạng";
            }

            var today = DateTime.Today;
            var startDate = new DateTime(today.Year, today.Month, 1);
            var row = (await _leaderboardService.BuildRowsAsync(startDate, startDate.AddMonths(1)))
                .FirstOrDefault(item => item.UserId == userId);

            if (row == null || row.Rank > 3)
            {
                return "Chưa vào top 3 tháng này";
            }

            return $"#{row.Rank} tháng này - {row.Score} điểm - {row.CompletedTaskCount} task";
        }
    }
}
