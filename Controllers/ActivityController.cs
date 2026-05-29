using System.Globalization;
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
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ActivityController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Challenge();
            }

            var now = DateTime.Now;
            var today = DateTime.Today;
            var tasks = await _context.TaskItems
                .Where(task => task.CreatedByUserId == currentUserId)
                .ToListAsync();
            var schedules = await _context.ScheduleItems
                .Where(schedule => schedule.CreatedByUserId == currentUserId)
                .ToListAsync();

            var model = new ActivityDashboardViewModel
            {
                UserEmail = User.Identity?.Name ?? string.Empty,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(task => task.Status == TaskItemStatus.Completed),
                OverdueTasks = tasks.Count(task => task.Status != TaskItemStatus.Completed && task.Deadline < now),
                ImportantSchedules = schedules.Count(schedule => schedule.IsImportant),
                DailyTasks = BuildDailyTasks(tasks, today),
                WeeklyTasks = BuildWeeklyTasks(tasks, today),
                MonthlyTasks = BuildMonthlyTasks(tasks, today),
                YearlyTasks = BuildYearlyTasks(tasks, today),
                CompletedTaskChart = BuildCompletedTasks(tasks, today),
                OverdueTaskChart = BuildOverdueTasks(tasks, today),
                ImportantScheduleChart = BuildImportantSchedules(schedules, today)
            };

            var streak = CalculateStreak(tasks
                .Where(task => task.Status == TaskItemStatus.Completed)
                .Select(task => task.UpdatedAt.Date)
                .Distinct()
                .ToHashSet());
            model.CurrentStreakDays = streak.Current;
            model.BestStreakDays = streak.Best;

            return View(model);
        }

        private static ActivityChartViewModel BuildDailyTasks(List<TaskItem> tasks, DateTime today)
        {
            var start = today.AddDays(-13);
            var labels = new List<string>();
            var values = new List<int>();

            for (var date = start; date <= today; date = date.AddDays(1))
            {
                labels.Add(date.ToString("dd/MM"));
                values.Add(tasks.Count(task => task.Deadline.Date == date.Date));
            }

            return new ActivityChartViewModel
            {
                Title = "Task theo ngày",
                Labels = labels,
                Values = values,
                Color = "#2563eb"
            };
        }

        private static ActivityChartViewModel BuildWeeklyTasks(List<TaskItem> tasks, DateTime today)
        {
            var labels = new List<string>();
            var values = new List<int>();
            var currentWeekStart = StartOfWeek(today);
            var firstWeekStart = currentWeekStart.AddDays(-7 * 7);

            for (var start = firstWeekStart; start <= currentWeekStart; start = start.AddDays(7))
            {
                var end = start.AddDays(7);
                labels.Add($"{start:dd/MM}");
                values.Add(tasks.Count(task => task.Deadline.Date >= start.Date && task.Deadline.Date < end.Date));
            }

            return new ActivityChartViewModel
            {
                Title = "Task theo tuần",
                Labels = labels,
                Values = values,
                Color = "#0f766e"
            };
        }

        private static ActivityChartViewModel BuildMonthlyTasks(List<TaskItem> tasks, DateTime today)
        {
            var labels = new List<string>();
            var values = new List<int>();
            var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

            for (var month = firstMonth; month <= new DateTime(today.Year, today.Month, 1); month = month.AddMonths(1))
            {
                labels.Add(month.ToString("MM/yyyy"));
                values.Add(tasks.Count(task => task.Deadline.Year == month.Year && task.Deadline.Month == month.Month));
            }

            return new ActivityChartViewModel
            {
                Title = "Task theo tháng",
                Labels = labels,
                Values = values,
                Color = "#7c3aed"
            };
        }

        private static ActivityChartViewModel BuildYearlyTasks(List<TaskItem> tasks, DateTime today)
        {
            var labels = new List<string>();
            var values = new List<int>();
            var firstYear = today.Year - 4;

            for (var year = firstYear; year <= today.Year; year++)
            {
                labels.Add(year.ToString(CultureInfo.InvariantCulture));
                values.Add(tasks.Count(task => task.Deadline.Year == year));
            }

            return new ActivityChartViewModel
            {
                Title = "Task theo năm",
                Labels = labels,
                Values = values,
                Color = "#f59e0b"
            };
        }

        private static ActivityChartViewModel BuildCompletedTasks(List<TaskItem> tasks, DateTime today)
        {
            var start = today.AddDays(-13);
            var labels = new List<string>();
            var values = new List<int>();

            for (var date = start; date <= today; date = date.AddDays(1))
            {
                labels.Add(date.ToString("dd/MM"));
                values.Add(tasks.Count(task => task.Status == TaskItemStatus.Completed && task.UpdatedAt.Date == date.Date));
            }

            return new ActivityChartViewModel
            {
                Title = "Task đã hoàn thành",
                Labels = labels,
                Values = values,
                Color = "#16a34a"
            };
        }

        private static ActivityChartViewModel BuildOverdueTasks(List<TaskItem> tasks, DateTime today)
        {
            var start = today.AddDays(-13);
            var labels = new List<string>();
            var values = new List<int>();

            for (var date = start; date <= today; date = date.AddDays(1))
            {
                labels.Add(date.ToString("dd/MM"));
                values.Add(tasks.Count(task => task.Status != TaskItemStatus.Completed && task.Deadline.Date == date.Date && task.Deadline < DateTime.Now));
            }

            return new ActivityChartViewModel
            {
                Title = "Task quá hạn",
                Labels = labels,
                Values = values,
                Color = "#dc2626"
            };
        }

        private static ActivityChartViewModel BuildImportantSchedules(List<ScheduleItem> schedules, DateTime today)
        {
            var labels = new List<string>();
            var values = new List<int>();
            var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

            for (var month = firstMonth; month <= new DateTime(today.Year, today.Month, 1); month = month.AddMonths(1))
            {
                labels.Add(month.ToString("MM/yyyy"));
                values.Add(schedules.Count(schedule => schedule.IsImportant && schedule.StartTime.Year == month.Year && schedule.StartTime.Month == month.Month));
            }

            return new ActivityChartViewModel
            {
                Title = "Lịch quan trọng",
                Labels = labels,
                Values = values,
                Color = "#db2777"
            };
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
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
