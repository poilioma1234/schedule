using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Helpers;
using schedule.Models;

namespace schedule.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var scheduleQuery = _context.ScheduleItems.AsNoTracking();
            var taskQuery = _context.TaskItems.AsNoTracking();

            if (!User.IsInRole("Admin"))
            {
                scheduleQuery = scheduleQuery.Where(item => item.CreatedByUserId == currentUserId);
                taskQuery = taskQuery.Where(item => item.CreatedByUserId == currentUserId);
            }

            ViewBag.TotalSchedules = await scheduleQuery.CountAsync();
            ViewBag.TotalTasks = await taskQuery.CountAsync();
            ViewBag.CompletedTasks = await taskQuery.CountAsync(item => item.Status == TaskItemStatus.Completed);
            ViewBag.OverdueTasks = await taskQuery.CountAsync(item => item.Status != TaskItemStatus.Completed && item.Deadline < DateTime.Now);

            // Build user list for admin PDF export modal
            if (User.IsInRole("Admin"))
            {
                var profileMap = await _context.UserProfiles
                    .AsNoTracking()
                    .ToDictionaryAsync(p => p.UserId, p => p.DisplayName);

                var allUsers = await _userManager.Users
                    .AsNoTracking()
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                var userList = allUsers.Select(u => (
                    Id: u.Id,
                    Email: u.Email ?? string.Empty,
                    DisplayName: profileMap.TryGetValue(u.Id, out var dn) ? dn ?? string.Empty : string.Empty
                )).ToList();

                ViewBag.Users = userList;
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            string? userId,
            string? fromDate,
            string? toDate,
            bool includeSchedules = true,
            bool includeTasks = true,
            bool includeActivity = true)
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Parse date range
            DateTime? from = string.IsNullOrWhiteSpace(fromDate) ? null : DateTime.TryParse(fromDate, out var fd) ? fd.Date : (DateTime?)null;
            DateTime? to = string.IsNullOrWhiteSpace(toDate) ? null : DateTime.TryParse(toDate, out var td) ? td.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;

            // Resolve owner
            string ownerEmail;
            string? targetUserId;
            if (isAdmin && !string.IsNullOrWhiteSpace(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                ownerEmail = targetUser?.Email ?? "user";
                targetUserId = userId;
            }
            else if (isAdmin && string.IsNullOrWhiteSpace(userId))
            {
                ownerEmail = "Tất cả người dùng";
                targetUserId = null;
            }
            else
            {
                ownerEmail = User.Identity?.Name ?? "user";
                targetUserId = currentUserId;
            }

            // Build schedule query
            IQueryable<ScheduleItem> scheduleQuery = _context.ScheduleItems.AsNoTracking();
            if (!isAdmin || !string.IsNullOrWhiteSpace(targetUserId))
            {
                var uid = targetUserId ?? currentUserId;
                scheduleQuery = scheduleQuery.Where(s => s.CreatedByUserId == uid);
            }
            if (from.HasValue) scheduleQuery = scheduleQuery.Where(s => s.StartTime >= from.Value);
            if (to.HasValue) scheduleQuery = scheduleQuery.Where(s => s.StartTime <= to.Value);

            // Build task query
            IQueryable<TaskItem> taskQuery = _context.TaskItems.AsNoTracking();
            if (!isAdmin || !string.IsNullOrWhiteSpace(targetUserId))
            {
                var uid = targetUserId ?? currentUserId;
                taskQuery = taskQuery.Where(t => t.CreatedByUserId == uid);
            }
            if (from.HasValue) taskQuery = taskQuery.Where(t => t.Deadline >= from.Value);
            if (to.HasValue) taskQuery = taskQuery.Where(t => t.Deadline <= to.Value);

            var schedules = includeSchedules
                ? await scheduleQuery.OrderBy(s => s.StartTime).ToListAsync()
                : new List<ScheduleItem>();

            var tasks = includeTasks
                ? await taskQuery.OrderBy(t => t.Deadline).ToListAsync()
                : new List<TaskItem>();

            // Activity summary from tasks (only when includeActivity)
            (int Total, int Completed, int Overdue, int InProgress)? activitySummary = includeActivity
                ? BuildActivitySummary(tasks)
                : null;

            var pdf = ReportPdfGenerator.Generate(
                schedules, tasks, activitySummary,
                ownerEmail, from, to,
                includeSchedules, includeTasks, includeActivity);

            var safeEmail = ownerEmail.Replace("/", "-").Replace("\\", "-");
            return File(pdf, "application/pdf", $"BaoCao_{safeEmail}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        private static (int Total, int Completed, int Overdue, int InProgress) BuildActivitySummary(List<TaskItem> tasks)
        {
            var now = DateTime.Now;
            var total = tasks.Count;
            var completed = tasks.Count(t => t.Status == TaskItemStatus.Completed);
            var overdue = tasks.Count(t => t.Status != TaskItemStatus.Completed && t.Deadline < now);
            var inProgress = tasks.Count(t => t.Status == TaskItemStatus.InProgress);
            return (total, completed, overdue, inProgress);
        }
    }
}
