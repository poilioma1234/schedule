using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;
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
        private readonly UserManager<IdentityUser> _userManager;

        public ProfileController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _environment = environment;
            _userManager = userManager;
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
            var youtubeEmbedUrl = TryBuildYouTubeEmbedUrl(profile.MusicUrl);

            return new ProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? "",
                IsOwner = isOwner,
                IsPublicProfile = isPublicProfile,
                PublicProfilePath = Url.Action(nameof(PublicProfile), "Profile", new { slug = profile.PublicSlug }) ?? $"/Profile/user/{profile.PublicSlug}",
                ShouldAutoplayMusic = isPublicProfile && !isOwner && !string.IsNullOrWhiteSpace(youtubeEmbedUrl),
                YouTubeEmbedUrl = youtubeEmbedUrl,
                Profile = profile,
                TotalSchedules = await scheduleQuery.CountAsync(),
                TodaySchedules = await scheduleQuery.CountAsync(item => item.StartTime.Date == DateTime.Today),
                ImportantSchedules = await scheduleQuery.CountAsync(item => item.IsImportant),
                ActiveOrUpcomingSchedules = await scheduleQuery.CountAsync(item => item.EndTime >= now),
                CompletedTaskCount = await taskQuery.CountAsync(item => item.Status == TaskItemStatus.Completed),
                RankLabel = "Chưa có dữ liệu xếp hạng"
            };
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
                PublicSlug = await CreateUniqueSlugAsync(user)
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

        private static string? TryBuildYouTubeEmbedUrl(string? url)
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
            return $"https://www.youtube.com/embed/{safeVideoId}?autoplay=1&loop=1&playlist={safeVideoId}&rel=0";
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
    }
}
