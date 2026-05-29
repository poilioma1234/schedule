using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Controllers
{
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

            if (!User.IsInRole("Admin"))
            {
                var currentUserId = _userManager.GetUserId(User);
                query = query.Where(item => item.CreatedByUserId == currentUserId);
                taskQuery = taskQuery.Where(task => task.CreatedByUserId == currentUserId);
            }

            model.TotalSchedules = await query.CountAsync();
            model.TodaySchedules = await query.CountAsync(item => item.StartTime.Date == today);
            model.ActiveSchedules = await query.CountAsync(item => item.StartTime <= now && item.EndTime >= now);
            model.UpcomingSchedules = await query.CountAsync(item => item.EndTime >= now);
            model.ImportantSchedules = await query.CountAsync(item => item.IsImportant);
            model.TodayTaskCount = await taskQuery.CountAsync(task => task.Deadline.Date == today);
            model.OverdueTaskCount = await taskQuery.CountAsync(task => task.Status != TaskItemStatus.Completed && task.Deadline < now);
            model.UpcomingItems = await query
                .Where(item => item.EndTime >= now)
                .OrderBy(item => item.StartTime)
                .Take(5)
                .ToListAsync();
            model.TodayTasks = await taskQuery
                .Where(task => task.Deadline.Date == today)
                .OrderBy(task => task.Deadline)
                .Take(6)
                .ToListAsync();
            model.OverdueTasks = await taskQuery
                .Where(task => task.Status != TaskItemStatus.Completed && task.Deadline < now)
                .OrderBy(task => task.Deadline)
                .Take(6)
                .ToListAsync();

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
