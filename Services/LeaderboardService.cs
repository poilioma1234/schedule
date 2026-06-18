using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Helpers;
using schedule.Models;

namespace schedule.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public LeaderboardService(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<LeaderboardEntry>> BuildRowsAsync(DateTime startDate, DateTime exclusiveEndDate)
        {
            var users = (await _userManager.GetUsersInRoleAsync("User"))
                .Where(user => !IsUserLocked(user))
                .ToList();
            var userIds = users.Select(user => user.Id).ToHashSet();
            var userLookup = users.ToDictionary(user => user.Id, user => user);
            var profiles = await _context.UserProfiles
                .AsNoTracking()
                .Where(profile => userIds.Contains(profile.UserId) && profile.IsProfilePublic)
                .ToListAsync();
            var publicUserIds = profiles.Select(profile => profile.UserId).ToHashSet();
            var completedTasks = await _context.TaskItems
                .AsNoTracking()
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
                    var displayName = string.IsNullOrWhiteSpace(profile.DisplayName)
                        ? user.Email ?? user.UserName ?? "User"
                        : profile.DisplayName;

                    return new LeaderboardEntry
                    {
                        UserId = profile.UserId,
                        Email = user.Email ?? user.UserName ?? "",
                        DisplayName = displayName,
                        AvatarPath = profile.AvatarPath,
                        PublicSlug = profile.PublicSlug ?? profile.UserId,
                        CompletedTaskCount = tasks.Count,
                        OnTimeTaskCount = tasks.Count(task => task.UpdatedAt <= task.Deadline),
                        UrgentTaskCount = tasks.Count(task => task.Priority == TaskPriorityLevel.Urgent),
                        Score = tasks.Sum(LeaderboardHelper.TaskScore)
                    };
                })
                .Where(row => row.CompletedTaskCount > 0 || row.Score > 0)
                .OrderByDescending(row => row.Score)
                .ThenByDescending(row => row.CompletedTaskCount)
                .ThenByDescending(row => row.OnTimeTaskCount)
                .ThenBy(row => row.DisplayName)
                .ToList();

            for (var index = 0; index < rows.Count; index++)
            {
                rows[index].Rank = index + 1;
            }

            return rows;
        }

        public async Task<List<LeaderboardAward>> EnsureMonthlyAwardsAsync(DateTime monthStart)
        {
            var periodStart = new DateTime(monthStart.Year, monthStart.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var exclusiveEnd = periodStart.AddMonths(1);

            if (exclusiveEnd > DateTime.Today)
            {
                return await LoadMonthlyAwardsAsync(periodStart);
            }

            var existing = await LoadMonthlyAwardsAsync(periodStart);
            if (existing.Any())
            {
                return existing;
            }

            var rows = (await BuildRowsAsync(periodStart, exclusiveEnd))
                .Take(3)
                .ToList();

            foreach (var row in rows)
            {
                _context.LeaderboardAwards.Add(new LeaderboardAward
                {
                    UserId = row.UserId,
                    UserEmailSnapshot = row.Email,
                    DisplayNameSnapshot = row.DisplayName,
                    Period = "month",
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Rank = row.Rank,
                    Score = row.Score,
                    CompletedTaskCount = row.CompletedTaskCount,
                    OnTimeTaskCount = row.OnTimeTaskCount,
                    AwardedAt = DateTime.Now
                });
            }

            if (rows.Any())
            {
                await _context.SaveChangesAsync();
            }

            return await LoadMonthlyAwardsAsync(periodStart);
        }

        public async Task EnsureAllHistoricalAwardsAsync()
        {
            var currentMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var start = new DateTime(2025, 5, 1);
            for (var m = start; m < currentMonthStart; m = m.AddMonths(1))
            {
                await EnsureMonthlyAwardsAsync(m);
            }
        }

        private Task<List<LeaderboardAward>> LoadMonthlyAwardsAsync(DateTime periodStart)
        {
            return _context.LeaderboardAwards
                .AsNoTracking()
                .Where(award => award.Period == "month" && award.PeriodStart == periodStart)
                .OrderBy(award => award.Rank)
                .ToListAsync();
        }

        private static bool IsUserLocked(IdentityUser user)
        {
            return user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        }
    }
}
