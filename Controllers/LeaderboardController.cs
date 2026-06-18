using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using schedule.Data;
using schedule.Services;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize]
    public class LeaderboardController : Controller
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardController(ApplicationDbContext context, ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        public async Task<IActionResult> Index(string period = "month", string? month = null, DateTime? date = null)
        {
            var normalizedPeriod = NormalizePeriod(period);
            var today = DateTime.Today;
            var selectedDate = date?.Date ?? today;
            var selectedMonth = ParseSelectedMonth(month, today);
            var (startDate, endDate, title) = GetPeriodRange(normalizedPeriod, today, selectedDate, selectedMonth);
            var exclusiveEndDate = endDate.Date.AddDays(1);

            var rows = (await _leaderboardService.BuildRowsAsync(startDate, exclusiveEndDate))
                .Select(row => new LeaderboardRowViewModel
                {
                    Rank = row.Rank,
                    UserId = row.UserId,
                    Email = row.Email,
                    DisplayName = row.DisplayName,
                    AvatarPath = row.AvatarPath,
                    PublicProfilePath = Url.Action("PublicProfile", "Profile", new { slug = row.PublicSlug }) ?? $"/Profile/user/{row.PublicSlug}",
                    CompletedTaskCount = row.CompletedTaskCount,
                    OnTimeTaskCount = row.OnTimeTaskCount,
                    OnTimeRate = row.OnTimeRate,
                    UrgentTaskCount = row.UrgentTaskCount,
                    Score = row.Score
                })
                .ToList();

            await _leaderboardService.EnsureAllHistoricalAwardsAsync();
            var monthlyAwards = normalizedPeriod == "month"
                ? await _leaderboardService.EnsureMonthlyAwardsAsync(selectedMonth)
                : new();

            var model = new LeaderboardViewModel
            {
                Period = normalizedPeriod,
                PeriodTitle = title,
                SelectedDate = selectedDate.ToString("yyyy-MM-dd"),
                SelectedMonth = selectedMonth.ToString("yyyy-MM"),
                StartDate = startDate,
                EndDate = endDate,
                IsFinalizedPeriod = normalizedPeriod == "month" && exclusiveEndDate <= today,
                LastUpdatedAt = DateTime.Now,
                Rows = rows,
                MonthlyAwards = monthlyAwards
            };

            return View(model);
        }

        private static string NormalizePeriod(string period)
        {
            return period.ToLowerInvariant() switch
            {
                "day" => "day",
                "week" => "week",
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
            DateTime selectedDate,
            DateTime selectedMonth)
        {
            return period switch
            {
                "day" => (selectedDate.Date, selectedDate.Date, $"Ngày {selectedDate:dd/MM/yyyy}"),
                "week" => (StartOfWeek(selectedDate), StartOfWeek(selectedDate).AddDays(6), $"Tuần {StartOfWeek(selectedDate):dd/MM} - {StartOfWeek(selectedDate).AddDays(6):dd/MM/yyyy}"),
                "year" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31), $"Năm {today:yyyy}"),
                _ => (selectedMonth, selectedMonth.AddMonths(1).AddDays(-1), $"Tháng {selectedMonth:MM/yyyy}")
            };
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }
    }
}
