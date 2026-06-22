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
    [ApiExplorerSettings(IgnoreApi = true)]
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
        public async Task<IActionResult> GetStats(
            string? userId,
            string? fromDate,
            string? toDate)
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var targetUserId = isAdmin && !string.IsNullOrWhiteSpace(userId) ? userId : currentUserId;

            DateTime? from = string.IsNullOrWhiteSpace(fromDate) ? null : DateTime.TryParse(fromDate, out var fd) ? fd.Date : (DateTime?)null;
            DateTime? to = string.IsNullOrWhiteSpace(toDate) ? null : DateTime.TryParse(toDate, out var td) ? td.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;

            IQueryable<ScheduleItem> scheduleQuery = _context.ScheduleItems.AsNoTracking().Where(s => s.CreatedByUserId == targetUserId);
            if (from.HasValue) scheduleQuery = scheduleQuery.Where(s => s.StartTime >= from.Value);
            if (to.HasValue) scheduleQuery = scheduleQuery.Where(s => s.StartTime <= to.Value);

            IQueryable<TaskItem> taskQuery = _context.TaskItems.AsNoTracking().Where(t => t.CreatedByUserId == targetUserId);
            if (from.HasValue) taskQuery = taskQuery.Where(t => t.Deadline >= from.Value);
            if (to.HasValue) taskQuery = taskQuery.Where(t => t.Deadline <= to.Value);

            var totalSchedules = await scheduleQuery.CountAsync();
            var totalTasks = await taskQuery.CountAsync();
            var completedTasks = await taskQuery.CountAsync(t => t.Status == TaskItemStatus.Completed);
            
            var now = DateTime.Now;
            var overdueTasks = await taskQuery.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline < now);
            var completedRate = totalTasks == 0 ? 0 : (int)Math.Round(completedTasks * 100.0 / totalTasks);

            return Json(new
            {
                totalSchedules,
                totalTasks,
                completedTasks,
                overdueTasks,
                completedRate
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            string? userId,
            string? fromDate,
            string? toDate,
            string reportType = "personal",
            bool includeSchedules = true,
            bool includeTasks = true,
            bool includeActivity = true,
            string mode = "download")
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Only Admin can request system overview or users list
            if (!isAdmin && (reportType == "system" || reportType == "users"))
            {
                reportType = "personal";
            }

            // Parse date range
            DateTime? from = string.IsNullOrWhiteSpace(fromDate) ? null : DateTime.TryParse(fromDate, out var fd) ? fd.Date : (DateTime?)null;
            DateTime? to = string.IsNullOrWhiteSpace(toDate) ? null : DateTime.TryParse(toDate, out var td) ? td.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;

            if (reportType == "system")
            {
                var stats = new SystemOverviewStats();
                
                // Users statistics
                stats.TotalUsers = await _context.Users.CountAsync();
                
                var adminRoleId = await _context.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
                stats.AdminUsers = adminRoleId != null ? await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId) : 0;
                stats.RegularUsers = stats.TotalUsers - stats.AdminUsers;
                
                stats.LockedUsers = await _context.Users.CountAsync(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow);
                stats.ActiveUsers = stats.TotalUsers - stats.LockedUsers;
                stats.PublicProfiles = await _context.UserProfiles.CountAsync(p => p.IsProfilePublic);
                stats.PendingReports = await _context.UserReports.CountAsync(r => r.Status == ReportStatus.Pending);

                // Schedules & Tasks query
                var systemSchedules = _context.ScheduleItems.AsNoTracking();
                var systemTasks = _context.TaskItems.AsNoTracking();
                if (from.HasValue)
                {
                    systemSchedules = systemSchedules.Where(s => s.StartTime >= from.Value);
                    systemTasks = systemTasks.Where(t => t.Deadline >= from.Value);
                }
                if (to.HasValue)
                {
                    systemSchedules = systemSchedules.Where(s => s.StartTime <= to.Value);
                    systemTasks = systemTasks.Where(t => t.Deadline <= to.Value);
                }

                stats.TotalSchedules = await systemSchedules.CountAsync();
                stats.TotalTasks = await systemTasks.CountAsync();
                stats.CompletedTasks = await systemTasks.CountAsync(t => t.Status == TaskItemStatus.Completed);
                stats.InProgressTasks = await systemTasks.CountAsync(t => t.Status == TaskItemStatus.InProgress);
                stats.OverdueTasks = await systemTasks.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline < DateTime.Now);

                var pdfSystem = ReportPdfGenerator.GenerateSystemOverview(stats, from, to);
                if (mode == "preview")
                {
                    return File(pdfSystem, "application/pdf");
                }
                return File(pdfSystem, "application/pdf", $"BaoCao_TongQuanHeThong_{DateTime.Now:yyyyMMdd}.pdf");
            }
            
            if (reportType == "users")
            {
                var usersList = await _userManager.Users.AsNoTracking().ToListAsync();
                var profileMap = await _context.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, p => p);
                
                var adminRoleId = await _context.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();
                var adminUserIds = await _context.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();

                var rows = new List<UserReportRow>();
                var now = DateTime.Now;

                foreach (var u in usersList)
                {
                    var isUserAdmin = adminUserIds.Contains(u.Id);
                    var displayName = profileMap.TryGetValue(u.Id, out var profile) ? profile.DisplayName : "";
                    
                    var userSchedules = _context.ScheduleItems.AsNoTracking().Where(s => s.CreatedByUserId == u.Id);
                    var userTasks = _context.TaskItems.AsNoTracking().Where(t => t.CreatedByUserId == u.Id);
                    
                    if (from.HasValue)
                    {
                        userSchedules = userSchedules.Where(s => s.StartTime >= from.Value);
                        userTasks = userTasks.Where(t => t.Deadline >= from.Value);
                    }
                    if (to.HasValue)
                    {
                        userSchedules = userSchedules.Where(s => s.StartTime <= to.Value);
                        userTasks = userTasks.Where(t => t.Deadline <= to.Value);
                    }

                    rows.Add(new UserReportRow
                    {
                        Email = u.Email ?? u.UserName ?? "",
                        DisplayName = displayName ?? "",
                        Roles = isUserAdmin ? "Admin" : "User",
                        IsLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                        ScheduleCount = await userSchedules.CountAsync(),
                        TaskCount = await userTasks.CountAsync(),
                        CompletedTaskCount = await userTasks.CountAsync(t => t.Status == TaskItemStatus.Completed),
                        OverdueTaskCount = await userTasks.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline < now),
                        CreatedAt = profile?.CreatedAt ?? DateTime.Now
                    });
                }

                var pdfUsers = ReportPdfGenerator.GenerateUsersReport(rows, from, to);
                if (mode == "preview")
                {
                    return File(pdfUsers, "application/pdf");
                }
                return File(pdfUsers, "application/pdf", $"BaoCao_DanhSachNguoiDung_{DateTime.Now:yyyyMMdd}.pdf");
            }

            // Resolve owner
            string ownerEmail;
            string? targetUserId;
            if (isAdmin && !string.IsNullOrWhiteSpace(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                ownerEmail = targetUser?.Email ?? "user";
                targetUserId = userId;
            }
            else
            {
                ownerEmail = User.Identity?.Name ?? "user";
                targetUserId = currentUserId;
            }

            // Override flags based on reportType
            if (reportType == "schedules")
            {
                includeSchedules = true;
                includeTasks = false;
                includeActivity = false;
            }
            else if (reportType == "tasks")
            {
                includeSchedules = false;
                includeTasks = true;
                includeActivity = false;
            }
            else if (reportType == "performance")
            {
                includeSchedules = false;
                includeTasks = false;
                includeActivity = true;
            }

            // Build schedule query
            IQueryable<ScheduleItem> scheduleQuery = _context.ScheduleItems.Include(s => s.Tasks).AsNoTracking();
            scheduleQuery = scheduleQuery.Where(s => s.CreatedByUserId == targetUserId);
            if (from.HasValue) scheduleQuery = scheduleQuery.Where(s => s.StartTime >= from.Value);
            if (to.HasValue) scheduleQuery = scheduleQuery.Where(s => s.StartTime <= to.Value);

            // Build task query
            IQueryable<TaskItem> taskQuery = _context.TaskItems.Include(t => t.ScheduleItem).AsNoTracking();
            taskQuery = taskQuery.Where(t => t.CreatedByUserId == targetUserId);
            if (from.HasValue) taskQuery = taskQuery.Where(t => t.Deadline >= from.Value);
            if (to.HasValue) taskQuery = taskQuery.Where(t => t.Deadline <= to.Value);

            var schedules = includeSchedules
                ? await scheduleQuery.OrderBy(s => s.StartTime).ToListAsync()
                : new List<ScheduleItem>();

            var tasks = (includeTasks || includeActivity)
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
            if (mode == "preview")
            {
                return File(pdf, "application/pdf");
            }
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
