using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    [Route("[controller]")]
    public class ProfileController : Controller
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private const long MaxImageSize = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILeaderboardService _leaderboardService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;

        public ProfileController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILeaderboardService leaderboardService,
            UserManager<IdentityUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _environment = environment;
            _leaderboardService = leaderboardService;
            _userManager = userManager;
            _emailService = emailService;
        }

        [Authorize]
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var profile = await GetOrCreateProfileAsync(user);
            var model = await BuildProfileViewModelAsync(user, profile, isPublicProfile: false);

            return View("Details", model);
        }

        [AllowAnonymous]
        [HttpGet("user/{slug}", Name = "PublicProfile")]
        public async Task<IActionResult> PublicProfile(string slug)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(item => item.PublicSlug == slug);
            if (profile == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(profile.UserId);
            if (user == null)
            {
                return NotFound();
            }

            var isOwner = User.Identity?.IsAuthenticated == true && _userManager.GetUserId(User) == user.Id;
            var canViewPrivateProfile = isOwner || User.IsInRole("Admin");
            if (!profile.IsProfilePublic && !canViewPrivateProfile)
            {
                return NotFound();
            }

            var model = await BuildProfileViewModelAsync(user, profile, isPublicProfile: true);
            return View("Details", model);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var profile = await GetOrCreateProfileAsync(user);
            return RedirectToAction(nameof(PublicProfile), new { slug = profile.PublicSlug });
        }

        [Authorize]
        [HttpGet("Edit")]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var profile = await GetOrCreateProfileAsync(user);
            var model = new EditProfileViewModel
            {
                DisplayName = profile.DisplayName,
                Bio = profile.Bio,
                IsProfilePublic = profile.IsProfilePublic,
                MusicUrl = profile.MusicUrl,
                FacebookUrl = profile.FacebookUrl,
                YouTubeUrl = profile.YouTubeUrl,
                TikTokUrl = profile.TikTokUrl,
                WebsiteUrl = profile.WebsiteUrl,
                CurrentAvatarPath = profile.AvatarPath,
                CurrentCoverPath = profile.CoverPath
            };

            return View(model);
        }

        [Authorize]
        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            ValidateImage(model.AvatarFile, nameof(model.AvatarFile));
            ValidateImage(model.CoverFile, nameof(model.CoverFile));

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var profile = await GetOrCreateProfileAsync(user);
            profile.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName)
                ? user.Email ?? user.UserName ?? "User"
                : model.DisplayName.Trim();
            profile.Bio = model.Bio?.Trim();
            profile.IsProfilePublic = model.IsProfilePublic;
            profile.MusicUrl = model.MusicUrl?.Trim();
            profile.FacebookUrl = model.FacebookUrl?.Trim();
            profile.YouTubeUrl = model.YouTubeUrl?.Trim();
            profile.TikTokUrl = model.TikTokUrl?.Trim();
            profile.WebsiteUrl = model.WebsiteUrl?.Trim();
            profile.UpdatedAt = DateTime.Now;

            if (string.IsNullOrWhiteSpace(profile.PublicSlug))
            {
                profile.PublicSlug = await CreateUniqueSlugAsync(user);
            }

            if (model.AvatarFile != null)
            {
                profile.AvatarPath = await SaveProfileImageAsync(user.Id, model.AvatarFile, "avatar");
            }

            if (model.CoverFile != null)
            {
                profile.CoverPath = await SaveProfileImageAsync(user.Id, model.CoverFile, "cover");
            }

            await _context.SaveChangesAsync();
            TempData["ProfileMessage"] = "Đã cập nhật hồ sơ.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<ProfileViewModel> BuildProfileViewModelAsync(
            IdentityUser user,
            UserProfile profile,
            bool isPublicProfile)
        {
            var scheduleQuery = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);
            var taskQuery = _context.TaskItems.Where(item => item.CreatedByUserId == user.Id);
            var now = DateTime.Now;
            var isOwner = User.Identity?.IsAuthenticated == true && _userManager.GetUserId(User) == user.Id;
            var isLocked = IsUserLocked(user);
            var shouldAutoplayMusic = !isLocked && isPublicProfile && !isOwner;
            var youtubeEmbedUrl = TryBuildYouTubeEmbedUrl(profile.MusicUrl, shouldAutoplayMusic);
            var tasks = await taskQuery.ToListAsync();
            var streak = ActivityStatsHelper.CalculateCompletionStreak(tasks, DateTime.Today);
            var rankLabel = await BuildCurrentRankLabelAsync(user.Id, profile.IsProfilePublic, isLocked);
            await _leaderboardService.EnsureAllHistoricalAwardsAsync();
            var awards = await _context.LeaderboardAwards
                .AsNoTracking()
                .Where(award => award.UserId == user.Id)
                .OrderByDescending(award => award.PeriodStart)
                .ThenBy(award => award.Rank)
                .ToListAsync();

            return new ProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? "",
                IsOwner = isOwner,
                IsLocked = isLocked,
                IsPublicProfile = isPublicProfile,
                PublicProfilePath = Url.Action(nameof(PublicProfile), "Profile", new { slug = profile.PublicSlug }) ?? $"/Profile/user/{profile.PublicSlug}",
                ShouldAutoplayMusic = shouldAutoplayMusic && !string.IsNullOrWhiteSpace(youtubeEmbedUrl),
                YouTubeEmbedUrl = youtubeEmbedUrl,
                Profile = profile,
                TotalSchedules = await scheduleQuery.CountAsync(),
                TodaySchedules = await scheduleQuery.CountAsync(item => item.StartTime.Date == DateTime.Today),
                ImportantSchedules = await scheduleQuery.CountAsync(item => item.IsImportant),
                ActiveOrUpcomingSchedules = await scheduleQuery.CountAsync(item => item.EndTime >= now),
                TotalTasks = tasks.Count,
                CompletedTaskCount = tasks.Count(item => item.Status == TaskItemStatus.Completed),
                OverdueTaskCount = tasks.Count(item => item.Status != TaskItemStatus.Completed && item.Deadline < now),
                CurrentStreakDays = streak.Current,
                LongestStreakDays = streak.Longest,
                CompletedTaskChart = ActivityStatsHelper.BuildCompletedTasksByDay(tasks, DateTime.Today, 30),
                RankLabel = rankLabel,
                LeaderboardAwards = awards,
                LatestAward = awards.FirstOrDefault()
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

        private async Task<string> BuildRankLabelAsync(string userId, bool isProfilePublic, bool isLocked)
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
            var exclusiveEndDate = startDate.AddMonths(1);
            var users = (await _userManager.GetUsersInRoleAsync("User"))
                .Where(user => !IsUserLocked(user))
                .ToList();
            var userIds = users.Select(user => user.Id).ToHashSet();
            var publicUserIds = await _context.UserProfiles
                .Where(profile => userIds.Contains(profile.UserId) && profile.IsProfilePublic)
                .Select(profile => profile.UserId)
                .ToListAsync();
            var tasks = await _context.TaskItems
                .Where(task =>
                    task.CreatedByUserId != null
                    && publicUserIds.Contains(task.CreatedByUserId)
                    && task.Status == TaskItemStatus.Completed
                    && task.UpdatedAt >= startDate
                    && task.UpdatedAt < exclusiveEndDate)
                .ToListAsync();

            var rows = publicUserIds
                .Select(id => new
                {
                    UserId = id,
                    Completed = tasks.Count(task => task.CreatedByUserId == id),
                    Score = tasks
                        .Where(task => task.CreatedByUserId == id)
                        .Sum(task => LeaderboardHelper.PriorityScore(task.Priority))
                })
                .Where(row => row.Score > 0)
                .OrderByDescending(row => row.Score)
                .ThenByDescending(row => row.Completed)
                .Take(3)
                .ToList();

            var index = rows.FindIndex(row => row.UserId == userId);
            if (index < 0)
            {
                return "Chưa vào top 3 tháng này";
            }

            var row = rows[index];
            return $"#{index + 1} tháng này - {row.Score} điểm - {row.Completed} task";
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

        private static string? TryBuildYouTubeEmbedUrl(string? url, bool autoplay)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var host = uri.Host.ToLowerInvariant();
            string? videoId = null;

            if (host.Contains("youtu.be"))
            {
                videoId = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
            }
            else if (host.Contains("youtube.com"))
            {
                if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
                {
                    videoId = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
                }
                else if (uri.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase))
                {
                    videoId = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
                }
                else
                {
                    videoId = uri.Query.TrimStart('?')
                        .Split('&', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Split('=', 2))
                        .Where(parts => parts.Length == 2 && parts[0] == "v")
                        .Select(parts => Uri.UnescapeDataString(parts[1]))
                        .FirstOrDefault();
                }
            }

            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            var safeVideoId = Uri.EscapeDataString(videoId);
            var autoplayValue = autoplay ? "1" : "0";
            return $"https://www.youtube.com/embed/{safeVideoId}?autoplay={autoplayValue}&loop=1&playlist={safeVideoId}&rel=0";
        }

        private void ValidateImage(IFormFile? file, string fieldName)
        {
            if (file == null)
            {
                return;
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(fieldName, "Chỉ hỗ trợ ảnh .jpg, .jpeg, .png, .webp hoặc .gif.");
            }

            if (file.Length > MaxImageSize)
            {
                ModelState.AddModelError(fieldName, "Ảnh không được vượt quá 5MB.");
            }
        }

        private async Task<string> SaveProfileImageAsync(string userId, IFormFile file, string prefix)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{prefix}-{Guid.NewGuid():N}{extension}";
            var relativeFolder = Path.Combine("uploads", "profiles", userId);
            var absoluteFolder = Path.Combine(_environment.WebRootPath, relativeFolder);

            Directory.CreateDirectory(absoluteFolder);

            var absolutePath = Path.Combine(absoluteFolder, fileName);
            await using var stream = System.IO.File.Create(absolutePath);
            await file.CopyToAsync(stream);

            return "/" + Path.Combine(relativeFolder, fileName).Replace("\\", "/");
        }

        private static bool IsUserLocked(IdentityUser user)
        {
            return user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        }

        // ── Report User ──────────────────────────────────────────────────────────
        [Authorize]
        [HttpPost("Report")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReport(string reportedUserId, string category, string reason)
        {
            if (string.IsNullOrWhiteSpace(reportedUserId) || string.IsNullOrWhiteSpace(reason))
                return BadRequest(new { success = false, message = "Thiếu thông tin báo cáo." });

            var reporterId = _userManager.GetUserId(User);

            // Không cho phép tự báo cáo bản thân
            if (reporterId == reportedUserId)
                return BadRequest(new { success = false, message = "Không thể tự báo cáo bản thân." });

            // Chống spam: mỗi user chỉ báo cáo 1 user tối đa 1 lần / 24h
            var recentReport = await _context.UserReports
                .Where(r => r.ReporterUserId == reporterId && r.ReportedUserId == reportedUserId
                         && r.CreatedAt > DateTime.Now.AddHours(-24))
                .AnyAsync();

            if (recentReport)
                return BadRequest(new { success = false, message = "Bạn đã báo cáo người dùng này trong 24 giờ qua. Vui lòng chờ thêm." });

            var report = new UserReport
            {
                ReportedUserId = reportedUserId,
                ReporterUserId = reporterId,
                Category = string.IsNullOrWhiteSpace(category) ? "other" : category,
                Reason = reason.Trim()[..Math.Min(reason.Trim().Length, 1000)],
                CreatedAt = DateTime.Now,
                Status = ReportStatus.Pending
            };

            _context.UserReports.Add(report);
            await _context.SaveChangesAsync();

            // Thông báo cho admin qua email
            try
            {
                var reportedUser = await _userManager.FindByIdAsync(reportedUserId);
                var reporterUser = await _userManager.FindByIdAsync(reporterId ?? "");
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                var adminEmail = adminUsers.FirstOrDefault()?.Email;

                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    var categoryLabel = category switch
                    {
                        "spam" => "Spam / Quảng cáo",
                        "harassment" => "Quấy rối / Ngôn từ không phù hợp",
                        "fake" => "Giả mạo danh tính",
                        "inappropriate" => "Nội dung không phù hợp",
                        _ => "Khác"
                    };

                    await _emailService.SendEmailAsync(
                        adminEmail,
                        "[HUTECH Schedule] Có báo cáo người dùng mới",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#dc2626;'>&#9888; Báo cáo người dùng</h2>" +
                        $"<p><strong>Người bị báo cáo:</strong> {reportedUser?.Email ?? reportedUserId}</p>" +
                        $"<p><strong>Người báo cáo:</strong> {reporterUser?.Email ?? "Ẩn danh"}</p>" +
                        $"<p><strong>Danh mục:</strong> {categoryLabel}</p>" +
                        "<p><strong>Lý do:</strong></p>" +
                        $"<blockquote style='border-left:4px solid #dc2626;padding:12px;background:#fef2f2;border-radius:6px;'>{System.Net.WebUtility.HtmlEncode(reason)}</blockquote>" +
                        $"<p><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "<a href='/Admin?section=notifications' style='display:inline-block;padding:10px 20px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;margin-top:12px;'>Xem tại Admin Dashboard</a>" +
                        "</div>");
                }
            }
            catch { /* không để lỗi email làm hỏng luồng */ }

            return Ok(new { success = true, message = "Báo cáo đã được gửi. Admin sẽ xem xét trong thời gian sớm nhất." });
        }
    }
}
