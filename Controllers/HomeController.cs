using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(
            ApplicationDbContext context,
            ILogger<HomeController> logger,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var model = new HomeDashboardViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                UserEmail = User.Identity?.Name ?? string.Empty
            };

            if (!model.IsAuthenticated)
            {
                return View(model);
            }

            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            var today = DateTime.Today;
            var now = DateTime.Now;
            var query = _context.ScheduleItems.AsQueryable();
            var taskQuery = _context.TaskItems
                .Include(task => task.ScheduleItem)
                .AsQueryable();

            var currentUserId = _userManager.GetUserId(User);
            query = query.Where(item => item.CreatedByUserId == currentUserId);
            taskQuery = taskQuery.Where(task => task.CreatedByUserId == currentUserId);

            model.TotalSchedules = await query.CountAsync(item => item.StartTime.Date == today);
            model.TodaySchedules = await query.CountAsync(item => item.StartTime.Date == today);
            model.ActiveSchedules = await query.CountAsync(item => item.StartTime <= now && item.EndTime >= now);
            model.UpcomingSchedules = await query.CountAsync(item => item.EndTime >= now);
            model.ImportantSchedules = await query.CountAsync(item => item.IsImportant && item.StartTime.Date == today);
            model.TodayTaskCount = await taskQuery.CountAsync(task => task.Deadline.Date == today);
            model.OverdueTaskCount = await taskQuery.CountAsync(task => task.Status != TaskItemStatus.Completed && task.Deadline.Date == today && task.Deadline < now);
            model.CompletedTaskCount = await taskQuery.CountAsync(task => task.Status == TaskItemStatus.Completed && task.Deadline.Date == today);
            model.InProgressTaskCount = await taskQuery.CountAsync(task => task.Status == TaskItemStatus.InProgress && task.Deadline.Date == today);
            model.PendingTaskCount = await taskQuery.CountAsync(task => task.Status == TaskItemStatus.NotStarted && task.Deadline.Date == today);
            model.UpcomingItems = await query
                .Where(item => item.StartTime.Date == today)
                .OrderBy(item => item.StartTime)
                .Take(5)
                .ToListAsync();
            model.TodayTasks = await taskQuery
                .Where(task => task.Deadline.Date == today)
                .OrderBy(task => task.Deadline)
                .Take(6)
                .ToListAsync();
            model.OverdueTasks = await taskQuery
                .Where(task => task.Status != TaskItemStatus.Completed && task.Deadline.Date == today && task.Deadline < now)
                .OrderBy(task => task.Deadline)
                .Take(6)
                .ToListAsync();
            model.Reminders = await query
                .Where(item => item.StartTime.Date == today && item.StartTime >= now)
                .OrderBy(item => item.StartTime)
                .Take(3)
                .ToListAsync();
            var recentTasks = await taskQuery
                .OrderByDescending(task => task.UpdatedAt)
                .Take(3)
                .ToListAsync();
            model.RecentActivities = recentTasks
                .Select(task =>
                {
                    var diff = now - task.UpdatedAt;
                    string elapsed;
                    if (diff.TotalMinutes < 1) elapsed = "vừa xong";
                    else if (diff.TotalHours < 1) elapsed = $"{(int)Math.Floor(diff.TotalMinutes)} phút trước";
                    else if (diff.TotalDays < 1) elapsed = $"{(int)Math.Floor(diff.TotalHours)} giờ trước";
                    else elapsed = $"{(int)Math.Floor(diff.TotalDays)} ngày trước";

                    return new HomeActivityItemViewModel
                    {
                        Title = task.Status == TaskItemStatus.Completed
                            ? $"Bạn đã hoàn thành task \"{task.Title}\""
                            : $"Bạn đã cập nhật task \"{task.Title}\"",
                        Detail = (task.ScheduleItem?.Title ?? "Không rõ lịch") + " · " + elapsed,
                        OccurredAt = task.UpdatedAt,
                        Tone = task.Status == TaskItemStatus.Completed ? "green" : task.Deadline < now ? "red" : "blue"
                    };
                })
                .ToList();

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
