using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;

        public SettingsController(
            ApplicationDbContext context,
            IConfiguration configuration,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var profile = await GetOrCreateProfileAsync(user);
            return View(new SettingsViewModel
            {
                Email = user.Email ?? user.UserName ?? string.Empty,
                DisplayName = profile.DisplayName,
                IsProfilePublic = profile.IsProfilePublic,
                EmailReminderEnabled = _configuration.GetValue<bool>("EmailSettings:Enabled"),
                PublicProfilePath = Url.Action("PublicProfile", "Profile", new { slug = profile.PublicSlug }) ?? $"/Profile/user/{profile.PublicSlug}"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfileVisibility(bool isProfilePublic)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var profile = await GetOrCreateProfileAsync(user);
            profile.IsProfilePublic = isProfilePublic;
            profile.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["SettingsMessage"] = "Đã cập nhật quyền riêng tư profile.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<UserProfile> GetOrCreateProfileAsync(IdentityUser user)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id);
            if (profile != null)
            {
                return profile;
            }

            profile = new UserProfile
            {
                UserId = user.Id,
                DisplayName = user.Email ?? user.UserName ?? "User",
                PublicSlug = Guid.NewGuid().ToString("N"),
                IsProfilePublic = true
            };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return profile;
        }
    }
}
