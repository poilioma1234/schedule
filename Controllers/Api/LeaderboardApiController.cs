using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using schedule.Data;
using schedule.Services;
using schedule.ViewModels;

namespace schedule.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/leaderboard")]
    public class LeaderboardApiController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardApiController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<LeaderboardRowViewModel>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeaderboard([FromQuery] string period = "month", [FromQuery] DateTime? date = null, [FromQuery] string? month = null)
        {
            var normalizedPeriod = NormalizePeriod(period);
            var today = DateTime.Today;
            var selectedDate = date?.Date ?? today;
            var selectedMonth = ParseSelectedMonth(month, today);
            var (startDate, endDate, _) = GetPeriodRange(normalizedPeriod, today, selectedDate, selectedMonth);
            var exclusiveEndDate = endDate.Date.AddDays(1);

            var rows = await _leaderboardService.BuildRowsAsync(startDate, exclusiveEndDate);
            return Ok(rows);
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
                "day" => (selectedDate, selectedDate, $"Ngày {selectedDate:dd/MM/yyyy}"),
                "week" => GetWeekRange(selectedDate),
                "year" => (new DateTime(selectedDate.Year, 1, 1), new DateTime(selectedDate.Year, 12, 31), $"Năm {selectedDate.Year}"),
                _ => (selectedMonth, selectedMonth.AddMonths(1).AddDays(-1), $"Tháng {selectedMonth:MM/yyyy}")
            };
        }

        private static (DateTime, DateTime, string) GetWeekRange(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            var start = date.AddDays(-1 * diff).Date;
            var end = start.AddDays(6).Date;
            return (start, end, $"Tuần {start:dd/MM} - {end:dd/MM/yyyy}");
        }
    }
}
