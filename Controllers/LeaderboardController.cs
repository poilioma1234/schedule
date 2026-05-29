using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Helpers;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize]
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public LeaderboardController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string period = "month", string? month = null)
        {
            var normalizedPeriod = NormalizePeriod(period);
            var selectedMonth = ParseSelectedMonth(month, DateTime.Today);
            var (startDate, endDate, title) = GetPeriodRange(normalizedPeriod, DateTime.Today, selectedMonth);
            var exclusiveEndDate = endDate.Date.AddDays(1);
            var users = (await _userManager.GetUsersInRoleAsync("User"))
                .Where(user => !IsUserLocked(user))
                .ToList();
            var userIds = users.Select(user => user.Id).ToHashSet();
            var userLookup = users.ToDictionary(user => user.Id, user => user);
            var profiles = await _context.UserProfiles
                .Where(profile => userIds.Contains(profile.UserId) && profile.IsProfilePublic)
                .ToListAsync();
            var publicUserIds = profiles.Select(profile => profile.UserId).ToHashSet();
            var completedTasks = await _context.TaskItems
                .Where(task =>
                    task.CreatedByUserId != null
                    && publicUserIds.Contains(task.CreatedByUserId)
                    && task.Status == TaskItemStatus.Completed
                    && task.UpdatedAt >= startDate.Date
                    && task.UpdatedAt < exclusiveEndDate)
                .ToListAsync();

            var rows = profiles
                .Select(profile =>
                {
                    var tasks = completedTasks
                        .Where(task => task.CreatedByUserId == profile.UserId)
                        .ToList();
                    var user = userLookup[profile.UserId];

                    return new LeaderboardRowViewModel
                    {
                        UserId = profile.UserId,
                        Email = user.Email ?? user.UserName ?? "",
                        DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName)
                            ? user.Email ?? user.UserName ?? "User"
                            : profile.DisplayName,
                        AvatarPath = profile.AvatarPath,
                        PublicProfilePath = Url.Action("PublicProfile", "Profile", new { slug = profile.PublicSlug }) ?? $"/Profile/user/{profile.PublicSlug}",
                        CompletedTaskCount = tasks.Count,
                        UrgentTaskCount = tasks.Count(task => task.Priority == TaskPriorityLevel.Urgent),
                        Score = tasks.Sum(task => LeaderboardHelper.PriorityScore(task.Priority))
                    };
                })
                .Where(row => row.CompletedTaskCount > 0 || row.Score > 0)
                .OrderByDescending(row => row.Score)
                .ThenByDescending(row => row.CompletedTaskCount)
                .ThenBy(row => row.DisplayName)
                .Take(3)
                .ToList();

            for (var index = 0; index < rows.Count; index++)
            {
                rows[index].Rank = index + 1;
            }

            var model = new LeaderboardViewModel
            {
                Period = normalizedPeriod,
                PeriodTitle = title,
                SelectedMonth = selectedMonth.ToString("yyyy-MM"),
                StartDate = startDate,
                EndDate = endDate,
                Rows = rows
            };

            return View(model);
        }

        private static string NormalizePeriod(string period)
        {
            return period.ToLowerInvariant() switch
            {
                "day" => "day",
                "year" => "year",
                _ => "month"
            };
        }

        private static DateTime ParseSelectedMonth(string? month, DateTime today)
        {
            if (DateTime.TryParseExact(
                month,
                "yyyy-MM",
                null,
                System.Globalization.DateTimeStyles.None,
                out var selectedMonth))
            {
                return new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            }

            return new DateTime(today.Year, today.Month, 1);
        }

        private static (DateTime StartDate, DateTime EndDate, string Title) GetPeriodRange(
            string period,
            DateTime today,
            DateTime selectedMonth)
        {
            return period switch
            {
                "day" => (today.Date, today.Date, $"Hôm nay {today:dd/MM/yyyy}"),
                "year" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31), $"Năm {today:yyyy}"),
                _ => (selectedMonth, selectedMonth.AddMonths(1).AddDays(-1), $"Tháng {selectedMonth:MM/yyyy}")
            };
        }

        private static bool IsUserLocked(IdentityUser user)
        {
            return user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        }
    }
}
