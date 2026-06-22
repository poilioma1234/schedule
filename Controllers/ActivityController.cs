using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ActivityController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            string rangePreset = "thismonth",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string groupBy = "auto",
            bool comparePrev = true)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Challenge();
            }

            var today = DateTime.Today;
            var now = DateTime.Now;

            // 1. Resolve date range
            DateTime startResolved;
            DateTime endResolved;

            switch (rangePreset.ToLowerInvariant())
            {
                case "7days":
                    endResolved = today;
                    startResolved = today.AddDays(-6);
                    break;
                case "14days":
                    endResolved = today;
                    startResolved = today.AddDays(-13);
                    break;
                case "30days":
                    endResolved = today;
                    startResolved = today.AddDays(-29);
                    break;
                case "lastmonth":
                    var lm = today.AddMonths(-1);
                    startResolved = new DateTime(lm.Year, lm.Month, 1);
                    endResolved = new DateTime(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month));
                    break;
                case "thismonth":
                default:
                    rangePreset = "thismonth";
                    startResolved = new DateTime(today.Year, today.Month, 1);
                    endResolved = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;
                case "custom":
                    startResolved = startDate?.Date ?? new DateTime(today.Year, today.Month, 1);
                    endResolved = endDate?.Date ?? today;
                    if (startResolved > endResolved)
                    {
                        var temp = startResolved;
                        startResolved = endResolved;
                        endResolved = temp;
                    }
                    // Limit duration to 1 year max for performance safety
                    if ((endResolved - startResolved).TotalDays > 366)
                    {
                        startResolved = endResolved.AddDays(-365);
                    }
                    break;
            }

            // 2. Resolve grouping
            var diffDays = (endResolved - startResolved).TotalDays + 1;
            var resolvedGroupBy = groupBy.ToLowerInvariant();
            if (resolvedGroupBy == "auto")
            {
                if (diffDays <= 31)
                {
                    resolvedGroupBy = "day";
                }
                else if (diffDays <= 180)
                {
                    resolvedGroupBy = "week";
                }
                else
                {
                    resolvedGroupBy = "month";
                }
            }

            // 3. Calculate previous period of same duration
            var prevEndDate = startResolved.AddDays(-1);
            var prevStartDate = prevEndDate.AddDays(-diffDays + 1);

            // 4. Query database tasks
            var tasks = await _context.TaskItems
                .Where(t => t.CreatedByUserId == currentUserId)
                .ToListAsync();

            // Current period task list segmentations
            var tasksCreatedCurrent = tasks.Where(t => t.CreatedAt.Date >= startResolved && t.CreatedAt.Date <= endResolved).ToList();
            var tasksCompletedCurrent = tasks.Where(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt.Date >= startResolved && t.UpdatedAt.Date <= endResolved).ToList();
            var tasksOverdueCurrent = tasks.Where(t => t.Status != TaskItemStatus.Completed && t.Deadline.Date >= startResolved && t.Deadline.Date <= endResolved && t.Deadline < now).ToList();

            // Previous period task list segmentations
            var tasksCreatedPrev = tasks.Where(t => t.CreatedAt.Date >= prevStartDate && t.CreatedAt.Date <= prevEndDate).ToList();
            var tasksCompletedPrev = tasks.Where(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt.Date >= prevStartDate && t.UpdatedAt.Date <= prevEndDate).ToList();
            var tasksOverduePrev = tasks.Where(t => t.Status != TaskItemStatus.Completed && t.Deadline.Date >= prevStartDate && t.Deadline.Date <= prevEndDate && t.Deadline < now).ToList();

            // 5. Populate ViewModel basic KPIs
            var model = new ActivityDashboardViewModel
            {
                UserEmail = User.Identity?.Name ?? string.Empty,
                TotalTasks = tasksCreatedCurrent.Count,
                CompletedTasks = tasksCompletedCurrent.Count,
                OverdueTasks = tasksOverdueCurrent.Count,
                CompletionRate = tasksCreatedCurrent.Count > 0 
                    ? Math.Round((tasksCompletedCurrent.Count * 100.0 / tasksCreatedCurrent.Count), 1) 
                    : 0.0
            };

            // Previous period KPIs
            model.PrevTotalTasks = tasksCreatedPrev.Count;
            model.PrevCompletedTasks = tasksCompletedPrev.Count;
            model.PrevOverdueTasks = tasksOverduePrev.Count;
            model.PrevCompletionRate = tasksCreatedPrev.Count > 0 
                ? Math.Round((tasksCompletedPrev.Count * 100.0 / tasksCreatedPrev.Count), 1) 
                : 0.0;

            //deltas
            model.TotalTasksDiff = CalculatePercentChange(model.TotalTasks, model.PrevTotalTasks);
            model.CompletedTasksDiff = CalculatePercentChange(model.CompletedTasks, model.PrevCompletedTasks);
            model.OverdueTasksDiff = CalculatePercentChange(model.OverdueTasks, model.PrevOverdueTasks);
            model.CompletionRateDiff = Math.Round(model.CompletionRate - model.PrevCompletionRate, 1);

            // Streak calculations (all-time completed tasks)
            var streak = CalculateStreak(tasks
                .Where(t => t.Status == TaskItemStatus.Completed)
                .Select(t => t.UpdatedAt.Date)
                .Distinct()
                .ToHashSet());
            model.CurrentStreakDays = streak.Current;
            model.BestStreakDays = streak.Best;

            // Donut chart status distribution (for tasks created in the current period)
            model.DonutCompleted = tasksCreatedCurrent.Count(t => t.Status == TaskItemStatus.Completed);
            model.DonutOverdue = tasksCreatedCurrent.Count(t => t.Status != TaskItemStatus.Completed && t.Deadline < now);
            model.DonutInProgress = tasksCreatedCurrent.Count(t => t.Status == TaskItemStatus.InProgress && t.Deadline >= now);
            model.DonutPending = tasksCreatedCurrent.Count(t => t.Status == TaskItemStatus.NotStarted && t.Deadline >= now);

            // 6. Time Grouping & Interval Generation
            var intervals = GetTimeIntervals(startResolved, endResolved, resolvedGroupBy);

            for (int i = 0; i < intervals.Count; i++)
            {
                var interval = intervals[i];
                var created = tasks.Count(t => t.CreatedAt.Date >= interval.Start && t.CreatedAt.Date <= interval.End);
                var completed = tasks.Count(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt.Date >= interval.Start && t.UpdatedAt.Date <= interval.End);
                var overdue = tasks.Count(t => t.Status != TaskItemStatus.Completed && t.Deadline.Date >= interval.Start && t.Deadline.Date <= interval.End && t.Deadline < now);

                model.MainChartLabels.Add(interval.Label);
                model.MainChartCreated.Add(created);
                model.MainChartCompleted.Add(completed);
                model.MainChartOverdue.Add(overdue);

                double rate = created > 0 ? Math.Round((completed * 100.0 / created), 1) : 0.0;
                if (rate > 100.0) rate = 100.0;

                // Compare completion rate vs previous interval
                double rateChange = 0.0;
                if (i > 0)
                {
                    var prevInterval = intervals[i - 1];
                    var prevCreated = tasks.Count(t => t.CreatedAt.Date >= prevInterval.Start && t.CreatedAt.Date <= prevInterval.End);
                    var prevCompleted = tasks.Count(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt.Date >= prevInterval.Start && t.UpdatedAt.Date <= prevInterval.End);
                    double prevRate = prevCreated > 0 ? (prevCompleted * 100.0 / prevCreated) : 0.0;
                    rateChange = Math.Round(rate - prevRate, 1);
                }
                else
                {
                    var durationDays = (interval.End - interval.Start).Days + 1;
                    var prevStart = interval.Start.AddDays(-durationDays);
                    var prevEnd = interval.Start.AddDays(-1);
                    var prevCreated = tasks.Count(t => t.CreatedAt.Date >= prevStart && t.CreatedAt.Date <= prevEnd);
                    var prevCompleted = tasks.Count(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt.Date >= prevStart && t.UpdatedAt.Date <= prevEnd);
                    double prevRate = prevCreated > 0 ? (prevCompleted * 100.0 / prevCreated) : 0.0;
                    rateChange = Math.Round(rate - prevRate, 1);
                }

                model.TableDetails.Add(new PeriodDetailItem
                {
                    Label = interval.Label,
                    CreatedCount = created,
                    CompletedCount = completed,
                    OverdueCount = overdue,
                    CompletionRate = rate,
                    RateChangeComparedToPrev = rateChange
                });
            }

            // Năng suất theo nhóm thời gian
            model.GroupChartLabels = model.MainChartLabels;
            model.GroupChartCompleted = model.MainChartCompleted;
            var avgVal = model.GroupChartCompleted.Any() ? Math.Round(model.GroupChartCompleted.Average(), 1) : 0.0;
            string unit = resolvedGroupBy == "day" ? "ngày" : (resolvedGroupBy == "week" ? "tuần" : "tháng");
            model.GroupChartAverageText = $"Trung bình: {avgVal} task/{unit}";

            // Xu hướng hoàn thành tích lũy
            model.CumulativeChartLabels = model.MainChartLabels;
            int runningSum = 0;
            foreach (var comp in model.MainChartCompleted)
            {
                runningSum += comp;
                model.CumulativeChartValues.Add(runningSum);
            }

            // Previous period daily trend trends (for sparklines)
            // Limit daily trend to maximum 45 days to avoid layout rendering latency
            var sparklineStep = 1;
            if (diffDays > 45)
            {
                sparklineStep = (int)Math.Ceiling(diffDays / 45.0);
            }

            for (var d = 0; d < diffDays; d += sparklineStep)
            {
                var targetDate = prevStartDate.AddDays(d);
                if (targetDate > prevEndDate) break;

                var nextDate = prevStartDate.AddDays(d + sparklineStep - 1);
                if (nextDate > prevEndDate) nextDate = prevEndDate;

                model.PrevCreatedTrend.Add(tasks.Count(t => t.CreatedAt.Date >= targetDate && t.CreatedAt.Date <= nextDate));
                model.PrevCompletedTrend.Add(tasks.Count(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt.Date >= targetDate && t.UpdatedAt.Date <= nextDate));
                model.PrevOverdueTrend.Add(tasks.Count(t => t.Status != TaskItemStatus.Completed && t.Deadline.Date >= targetDate && t.Deadline.Date <= nextDate && t.Deadline < now));
            }

            // 7. Store filter state in ViewBag
            ViewBag.RangePreset = rangePreset;
            ViewBag.StartDateFormatted = startResolved.ToString("yyyy-MM-dd");
            ViewBag.EndDateFormatted = endResolved.ToString("yyyy-MM-dd");
            ViewBag.PrevPeriodLabel = $"{prevStartDate:dd/MM} - {prevEndDate:dd/MM}";
            ViewBag.GroupBy = groupBy;
            ViewBag.ComparePrev = comparePrev;
            ViewBag.ResolvedGroupBy = resolvedGroupBy;
            ViewBag.LastUpdatedTime = now.ToString("HH:mm dd/MM/yyyy");

            return View(model);
        }

        private static double CalculatePercentChange(double current, double previous)
        {
            if (previous <= 0)
            {
                return current > 0 ? 100.0 : 0.0;
            }
            return Math.Round(((current - previous) / previous) * 100.0, 1);
        }

        private static List<(DateTime Start, DateTime End, string Label)> GetTimeIntervals(DateTime start, DateTime end, string groupBy)
        {
            var intervals = new List<(DateTime Start, DateTime End, string Label)>();
            if (groupBy.Equals("day", StringComparison.OrdinalIgnoreCase))
            {
                for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
                {
                    intervals.Add((date, date, date.ToString("dd/MM")));
                }
            }
            else if (groupBy.Equals("week", StringComparison.OrdinalIgnoreCase))
            {
                var current = start.Date;
                var weekNum = 1;
                while (current <= end.Date)
                {
                    var next = current.AddDays(6);
                    if (next > end.Date)
                    {
                        next = end.Date;
                    }
                    intervals.Add((current, next, $"T{weekNum} ({current:dd/MM} - {next:dd/MM})"));
                    current = next.AddDays(1);
                    weekNum++;
                }
            }
            else // month
            {
                var current = new DateTime(start.Year, start.Month, 1);
                while (current <= end.Date)
                {
                    var monthEnd = new DateTime(current.Year, current.Month, DateTime.DaysInMonth(current.Year, current.Month));
                    var chunkStart = current < start.Date ? start.Date : current;
                    var chunkEnd = monthEnd > end.Date ? end.Date : monthEnd;
                    intervals.Add((chunkStart, chunkEnd, current.ToString("MM/yyyy")));
                    current = current.AddMonths(1);
                }
            }
            return intervals;
        }

        private static (int Current, int Best) CalculateStreak(HashSet<DateTime> activityDays)
        {
            if (!activityDays.Any())
            {
                return (0, 0);
            }

            var orderedDays = activityDays.OrderBy(day => day).ToList();
            var best = 1;
            var currentRun = 1;

            for (var index = 1; index < orderedDays.Count; index++)
            {
                if (orderedDays[index] == orderedDays[index - 1].AddDays(1))
                {
                    currentRun++;
                }
                else
                {
                    best = Math.Max(best, currentRun);
                    currentRun = 1;
                }
            }

            best = Math.Max(best, currentRun);

            var current = 0;
            var cursor = DateTime.Today;
            while (activityDays.Contains(cursor))
            {
                current++;
                cursor = cursor.AddDays(-1);
            }

            if (current == 0 && activityDays.Contains(DateTime.Today.AddDays(-1)))
            {
                cursor = DateTime.Today.AddDays(-1);
                while (activityDays.Contains(cursor))
                {
                    current++;
                    cursor = cursor.AddDays(-1);
                }
            }

            return (current, best);
        }
    }
}
