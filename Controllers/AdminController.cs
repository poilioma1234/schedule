using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;
using schedule.Services;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;

        public AdminController(
            ApplicationDbContext context,
            IConfiguration configuration,
            UserManager<IdentityUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index(string section = "overview", string? searchString = null, string statusFilter = "all")
        {
            var activeSection = NormalizeSection(section);
            var today = DateTime.Today;
            var now = DateTime.Now;
            var users = await _userManager.Users.OrderBy(user => user.Email).ToListAsync();
            var userRows = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var scheduleQuery = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);
                var taskQuery = _context.TaskItems.Where(item => item.CreatedByUserId == user.Id);
                var avatarPath = await _context.UserProfiles
                    .Where(profile => profile.UserId == user.Id)
                    .Select(profile => profile.AvatarPath)
                    .FirstOrDefaultAsync();

                userRows.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? user.UserName ?? "",
                    AvatarPath = avatarPath,
                    Roles = roles.Any() ? string.Join(", ", roles) : "User",
                    IsAdmin = roles.Contains("Admin"),
                    IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                    ScheduleCount = await scheduleQuery.CountAsync(),
                    TodayScheduleCount = await scheduleQuery.CountAsync(item => item.StartTime.Date == today),
                    ActiveOrUpcomingScheduleCount = await scheduleQuery.CountAsync(item => item.EndTime >= now),
                    TotalTaskCount = await taskQuery.CountAsync(),
                    CompletedTaskCount = await taskQuery.CountAsync(item => item.Status == TaskItemStatus.Completed),
                    OverdueTaskCount = await taskQuery.CountAsync(item => item.Status != TaskItemStatus.Completed && item.Deadline < now),
                    LastScheduleAt = await scheduleQuery
                        .OrderByDescending(item => item.CreatedAt)
                        .Select(item => (DateTime?)item.CreatedAt)
                        .FirstOrDefaultAsync()
                });
            }

            var filteredUsers = userRows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                filteredUsers = filteredUsers.Where(user =>
                    user.Email.Contains(searchString.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            filteredUsers = statusFilter switch
            {
                "locked" => filteredUsers.Where(user => user.IsLocked),
                "admin" => filteredUsers.Where(user => user.IsAdmin),
                "user" => filteredUsers.Where(user => !user.IsAdmin),
                _ => filteredUsers
            };

            var chartStartDate = today.AddDays(-6);
            var chartEndDate = today.AddDays(1);
            var priorStartDate = chartStartDate.AddDays(-7);

            var totalTasksCount = await _context.TaskItems.CountAsync();
            var completedTasksCount = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Completed);
            var overdueTasksCount = await _context.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline < now);

            var todayTasks = await _context.TaskItems.CountAsync(t => t.Deadline.Date == today);
            var yesterdayTasks = await _context.TaskItems.CountAsync(t => t.Deadline.Date == today.AddDays(-1));
            int todayTasksPct = yesterdayTasks == 0 ? (todayTasks > 0 ? 100 : 0) : (int)Math.Round((todayTasks - yesterdayTasks) * 100.0 / yesterdayTasks);
            var todayTasksChange = todayTasksPct >= 0 ? $"↑ {todayTasksPct}% so với hôm qua" : $"↓ {Math.Abs(todayTasksPct)}% so với hôm qua";

            var overdueTasksToday = overdueTasksCount;
            var overdueTasksYesterday = await _context.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline < today);
            int overdueTasksPct = overdueTasksYesterday == 0 ? (overdueTasksToday > 0 ? 100 : 0) : (int)Math.Round((overdueTasksToday - overdueTasksYesterday) * 100.0 / overdueTasksYesterday);
            var overdueTasksChange = overdueTasksPct >= 0 ? $"↑ {overdueTasksPct}% so với hôm qua" : $"↓ {Math.Abs(overdueTasksPct)}% so với hôm qua";

            var totalTasks7Days = await _context.TaskItems.CountAsync(t => t.CreatedAt >= chartStartDate);
            var completedTasks7DaysCount = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt >= chartStartDate);
            double currentRate = totalTasks7Days == 0 ? 0 : (completedTasks7DaysCount * 100.0 / totalTasks7Days);

            var totalTasksPrior7Days = await _context.TaskItems.CountAsync(t => t.CreatedAt >= priorStartDate && t.CreatedAt < chartStartDate);
            var completedTasksPrior7Days = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt >= priorStartDate && t.UpdatedAt < chartStartDate);
            double priorRate = totalTasksPrior7Days == 0 ? 0 : (completedTasksPrior7Days * 100.0 / totalTasksPrior7Days);

            int rateDiff = (int)Math.Round(currentRate - priorRate);
            var completedRateChange = rateDiff >= 0 ? $"↑ {rateDiff}% so với tuần trước" : $"↓ {Math.Abs(rateDiff)}% so với tuần trước";

            var schedulesForChart = await _context.ScheduleItems
                .Where(item => item.CreatedAt >= chartStartDate && item.CreatedAt < chartEndDate)
                .Select(item => item.CreatedAt)
                .ToListAsync();

            var createdTasksForChart = await _context.TaskItems
                .Where(item => item.CreatedAt >= chartStartDate && item.CreatedAt < chartEndDate)
                .Select(item => item.CreatedAt)
                .ToListAsync();

            var completedTasksForChart = await _context.TaskItems
                .Where(item => item.Status == TaskItemStatus.Completed
                    && item.UpdatedAt >= chartStartDate
                    && item.UpdatedAt < chartEndDate)
                .Select(item => item.UpdatedAt)
                .ToListAsync();

            var overdueTasksForChart = await _context.TaskItems
                .Where(item => item.Status != TaskItemStatus.Completed && item.Deadline >= chartStartDate && item.Deadline < chartEndDate)
                .Select(item => item.Deadline)
                .ToListAsync();

            // Calculate historical 7 days statistics
            var created7Days = createdTasksForChart.Count;
            var createdPrior7Days = await _context.TaskItems.CountAsync(t => t.CreatedAt >= priorStartDate && t.CreatedAt < chartStartDate);
            var createdPctVal = createdPrior7Days == 0 ? (created7Days > 0 ? 100 : 0) : (int)Math.Round((created7Days - createdPrior7Days) * 100.0 / createdPrior7Days);
            var created7DaysChange = createdPctVal >= 0 ? $"↑ {createdPctVal}% so với tuần trước" : $"↓ {Math.Abs(createdPctVal)}% so với tuần trước";

            var completed7Days = completedTasksForChart.Count;
            var completedPrior7Days = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt >= priorStartDate && t.UpdatedAt < chartStartDate);
            var completedPctVal = completedPrior7Days == 0 ? (completed7Days > 0 ? 100 : 0) : (int)Math.Round((completed7Days - completedPrior7Days) * 100.0 / completedPrior7Days);
            var completed7DaysChange = completedPctVal >= 0 ? $"↑ {completedPctVal}% so với tuần trước" : $"↓ {Math.Abs(completedPctVal)}% so với tuần trước";

            var overdue7Days = overdueTasksForChart.Count;
            var overduePrior7Days = await _context.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline >= priorStartDate && t.Deadline < chartStartDate);
            var overduePctVal = overduePrior7Days == 0 ? (overdue7Days > 0 ? 100 : 0) : (int)Math.Round((overdue7Days - overduePrior7Days) * 100.0 / overduePrior7Days);
            var overdue7DaysChange = overduePctVal >= 0 ? $"↑ {overduePctVal}% so với tuần trước" : $"↓ {Math.Abs(overduePctVal)}% so với tuần trước";

            var activityPoints = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var date = chartStartDate.AddDays(offset).Date;
                    return new AdminActivityPointViewModel
                    {
                        Date = date,
                        Label = date.ToString("dd/MM"),
                        ScheduleCount = schedulesForChart.Count(item => item.Date == date),
                        CreatedTaskCount = createdTasksForChart.Count(item => item.Date == date),
                        CompletedTaskCount = completedTasksForChart.Count(item => item.Date == date),
                        OverdueTaskCount = overdueTasksForChart.Count(item => item.Date == date)
                    };
                })
                .ToList();

            var upcomingSchedules = await _context.ScheduleItems
                .Where(item => item.EndTime >= now)
                .OrderBy(item => item.StartTime)
                .Take(5)
                .Select(item => new AdminUpcomingScheduleViewModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Location = item.Location,
                    OwnerEmail = item.CreatedByEmail,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime
                })
                .ToListAsync();

            foreach (var schedule in upcomingSchedules)
            {
                schedule.IsToday = schedule.StartTime.Date == today;
            }

            var overdueTasks = await _context.TaskItems
                .Where(item => item.Status != TaskItemStatus.Completed && (item.Deadline < now || item.Deadline <= now.AddHours(24)))
                .OrderBy(item => item.Deadline)
                .Take(5)
                .Select(item => new AdminOverdueTaskViewModel
                {
                    Id = item.Id,
                    ScheduleItemId = item.ScheduleItemId,
                    Title = item.Title,
                    OwnerEmail = item.CreatedByEmail,
                    Deadline = item.Deadline
                })
                .ToListAsync();

            foreach (var task in overdueTasks)
            {
                task.IsOverdue = task.Deadline < now;
                if (task.IsOverdue)
                {
                    var diff = now - task.Deadline;
                    if (diff.TotalDays >= 1)
                    {
                        task.DaysOverdue = (int)Math.Floor(diff.TotalDays);
                        task.AttentionText = $"Quá hạn {task.DaysOverdue} ngày";
                    }
                    else
                    {
                        var hrs = (int)Math.Ceiling(diff.TotalHours);
                        task.AttentionText = $"Quá hạn {hrs} giờ";
                    }
                }
                else
                {
                    var diff = task.Deadline - now;
                    if (diff.TotalHours >= 1)
                    {
                        var hrs = (int)Math.Floor(diff.TotalHours);
                        task.AttentionText = $"Còn {hrs} giờ · Hôm nay {task.Deadline:HH:mm}";
                    }
                    else
                    {
                        var mins = (int)Math.Ceiling(diff.TotalMinutes);
                        task.AttentionText = $"Còn {mins} phút · Hôm nay {task.Deadline:HH:mm}";
                    }
                }
            }

            var schedules = await _context.ScheduleItems
                .Include(item => item.Tasks)
                .OrderByDescending(item => item.StartTime)
                .Take(200)
                .Select(item => new AdminScheduleRowViewModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Location = item.Location,
                    OwnerEmail = item.CreatedByEmail,
                    OwnerUserId = item.CreatedByUserId,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    TaskCount = item.Tasks.Count,
                    IsImportant = item.IsImportant
                })
                .ToListAsync();

            var tasks = await _context.TaskItems
                .Include(item => item.ScheduleItem)
                .OrderByDescending(item => item.Deadline)
                .Take(200)
                .Select(item => new AdminTaskRowViewModel
                {
                    Id = item.Id,
                    ScheduleItemId = item.ScheduleItemId,
                    Title = item.Title,
                    ScheduleTitle = item.ScheduleItem != null ? item.ScheduleItem.Title : null,
                    OwnerEmail = item.CreatedByEmail,
                    Deadline = item.Deadline,
                    StatusLabel = item.Status.ToString(),
                    PriorityLabel = item.Priority.ToString(),
                    Color = item.Color,
                    IsOverdue = item.Status != TaskItemStatus.Completed && item.Deadline < now
                })
                .ToListAsync();

            foreach (var task in tasks)
            {
                task.StatusLabel = BuildStatusLabel(task.StatusLabel);
                task.PriorityLabel = BuildPriorityLabel(task.PriorityLabel);
            }

            var recentSchedulesList = await _context.ScheduleItems
                .OrderByDescending(item => item.CreatedAt)
                .Take(8)
                .Select(item => new
                {
                    Title = item.Title,
                    CreatedByEmail = item.CreatedByEmail,
                    CreatedAt = item.CreatedAt,
                    IsImportant = item.IsImportant
                })
                .ToListAsync();

            var recentScheduleEvents = recentSchedulesList.Select(item => new AdminActivityEventViewModel
            {
                Type = "Lịch trình",
                Title = $"Lịch trình \"{item.Title}\" được {(item.IsImportant ? "đánh dấu quan trọng" : "tạo mới")}",
                Detail = (item.CreatedByEmail ?? "Không rõ user") + " đã tạo lịch",
                OccurredAt = item.CreatedAt,
                Tone = item.IsImportant ? "purple" : "blue"
            }).ToList();

            var recentTasksList = await _context.TaskItems
                .OrderByDescending(item => item.UpdatedAt)
                .Take(8)
                .Select(item => new
                {
                    Title = item.Title,
                    CreatedByEmail = item.CreatedByEmail,
                    UpdatedAt = item.UpdatedAt,
                    Status = item.Status
                })
                .ToListAsync();

            var recentTaskEvents = recentTasksList.Select(item => new AdminActivityEventViewModel
            {
                Type = "Task",
                Title = (item.CreatedByEmail ?? "User") + " " + (item.Status == TaskItemStatus.Completed ? "đã hoàn thành task" : "đã cập nhật task"),
                Detail = item.Title,
                OccurredAt = item.UpdatedAt,
                Tone = item.Status == TaskItemStatus.Completed ? "green" : "blue"
            }).ToList();

            var recentActivities = recentScheduleEvents
                .Concat(recentTaskEvents)
                .OrderByDescending(item => item.OccurredAt)
                .Take(12)
                .ToList();

            foreach (var act in recentActivities)
            {
                var diff = now - act.OccurredAt;
                if (diff.TotalMinutes < 1)
                {
                    act.TimeElapsed = "vừa xong";
                }
                else if (diff.TotalHours < 1)
                {
                    act.TimeElapsed = $"{(int)Math.Floor(diff.TotalMinutes)} phút trước";
                }
                else if (diff.TotalDays < 1)
                {
                    act.TimeElapsed = $"{(int)Math.Floor(diff.TotalHours)} giờ trước";
                }
                else
                {
                    act.TimeElapsed = $"{(int)Math.Floor(diff.TotalDays)} ngày trước";
                }
            }

            // ── Load user reports (REAL notifications) ──
            var pendingReports = await _context.UserReports
                .Where(r => r.Status == ReportStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            var notifications = new List<AdminNotificationViewModel>();

            // System alerts come first
            if (overdueTasks.Any())
            {
                notifications.Add(new AdminNotificationViewModel
                {
                    Severity = "warning",
                    Title = $"{overdueTasks.Count} task quá hạn chưa xử lý",
                    Detail = "Mở tab Task để xem deadline và người phụ trách.",
                    ActionUrl = Url.Action(nameof(Index), "Admin", new { section = "tasks" }),
                    ActionLabel = "Xem task"
                });
            }

            var model = new AdminDashboardViewModel
            {
                ActiveSection = activeSection,
                TotalUsers = userRows.Count,
                ActiveUsers = userRows.Count(user => !user.IsLocked),
                LockedUsers = userRows.Count(user => user.IsLocked),
                AdminUsers = userRows.Count(user => user.IsAdmin),
                TotalSchedules = await _context.ScheduleItems.CountAsync(),
                TodaySchedules = await _context.ScheduleItems.CountAsync(item => item.StartTime.Date == today),
                ActiveOrUpcomingSchedules = await _context.ScheduleItems.CountAsync(item => item.EndTime >= now),
                TotalTasks = totalTasksCount,
                CompletedTasks = completedTasksCount,
                OverdueTasks = overdueTasksCount,
                TodayTasks = todayTasks,
                TodayTasksChange = todayTasksChange,
                OverdueTasksChange = overdueTasksChange,
                CompletedRateChange = completedRateChange,
                CreatedTasks7DaysChange = created7DaysChange,
                CompletedTasks7DaysChange = completed7DaysChange,
                OverdueTasks7DaysChange = overdue7DaysChange,
                EmailReminderEnabled = _configuration.GetValue<bool>("EmailSettings:EnableEmail"),
                SearchString = searchString ?? string.Empty,
                StatusFilter = statusFilter,
                Users = filteredUsers.ToList(),
                ActivityPoints = activityPoints,
                UpcomingSchedules = upcomingSchedules,
                OverdueTaskItems = overdueTasks,
                Schedules = schedules,
                Tasks = tasks,
                RecentActivities = recentActivities,
                Notifications = notifications,
                PendingReports = pendingReports
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportUsersPdf()
        {
            var now = DateTime.Now;
            var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
            var rows = new List<(string Email, string Roles, bool IsLocked, int Schedules, int Tasks)>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var isLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
                var schedCount = await _context.ScheduleItems.CountAsync(s => s.CreatedByUserId == u.Id);
                var taskCount = await _context.TaskItems.CountAsync(t => t.CreatedByUserId == u.Id);
                rows.Add((u.Email ?? u.UserName ?? "", string.Join(", ", roles.Any() ? roles : new[] { "User" }), isLocked, schedCount, taskCount));
            }
            var pdf = schedule.Helpers.AdminPdfGenerator.GenerateUserStats(rows, now);
            return File(pdf, "application/pdf", $"BaoCao_NguoiDung_{now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ExportReportPdf(
            string type = "tasks",
            string? from = null,
            string? to = null,
            string? userId = null,
            string? status = null)
        {
            var now = DateTime.Now;
            DateTime? fromDate = string.IsNullOrWhiteSpace(from) ? null : DateTime.TryParse(from, out var fd) ? fd.Date : (DateTime?)null;
            DateTime? toDate = string.IsNullOrWhiteSpace(to) ? null : DateTime.TryParse(to, out var td) ? td.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;

            IQueryable<schedule.Models.TaskItem> taskQ = _context.TaskItems.AsNoTracking();
            IQueryable<schedule.Models.ScheduleItem> schedQ = _context.ScheduleItems.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                taskQ = taskQ.Where(t => t.CreatedByUserId == userId);
                schedQ = schedQ.Where(s => s.CreatedByUserId == userId);
            }
            if (fromDate.HasValue) { taskQ = taskQ.Where(t => t.Deadline >= fromDate.Value); schedQ = schedQ.Where(s => s.StartTime >= fromDate.Value); }
            if (toDate.HasValue) { taskQ = taskQ.Where(t => t.Deadline <= toDate.Value); schedQ = schedQ.Where(s => s.StartTime <= toDate.Value); }

            if (!string.IsNullOrWhiteSpace(status))
            {
                taskQ = status switch
                {
                    "completed" => taskQ.Where(t => t.Status == schedule.Models.TaskItemStatus.Completed),
                    "overdue" => taskQ.Where(t => t.Status != schedule.Models.TaskItemStatus.Completed && t.Deadline < now),
                    "inprogress" => taskQ.Where(t => t.Status == schedule.Models.TaskItemStatus.InProgress),
                    "notstarted" => taskQ.Where(t => t.Status == schedule.Models.TaskItemStatus.NotStarted),
                    _ => taskQ
                };
            }

            var tasks = (type == "tasks" || type == "user-activity" || type == "full")
                ? await taskQ.OrderBy(t => t.Deadline).ToListAsync()
                : new List<schedule.Models.TaskItem>();

            var scheds = (type == "schedules" || type == "full")
                ? await schedQ.OrderBy(s => s.StartTime).ToListAsync()
                : new List<schedule.Models.ScheduleItem>();

            string ownerLabel = "Tất cả người dùng";
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var u = await _userManager.FindByIdAsync(userId);
                ownerLabel = u?.Email ?? userId;
            }

            (int Total, int Completed, int Overdue, int InProgress)? actSummary = null;
            if (type == "user-activity" || type == "full")
            {
                actSummary = (
                    tasks.Count,
                    tasks.Count(t => t.Status == schedule.Models.TaskItemStatus.Completed),
                    tasks.Count(t => t.Status != schedule.Models.TaskItemStatus.Completed && t.Deadline < now),
                    tasks.Count(t => t.Status == schedule.Models.TaskItemStatus.InProgress)
                );
            }

            bool inclSched = type == "schedules" || type == "full";
            bool inclTask = type == "tasks" || type == "user-activity" || type == "full";
            bool inclActivity = type == "user-activity" || type == "full";

            var pdf = schedule.Helpers.ReportPdfGenerator.Generate(
                scheds, tasks, actSummary, ownerLabel, fromDate, toDate,
                inclSched, inclTask, inclActivity);

            return File(pdf, "application/pdf", $"BaoCao_{type}_{now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ChartData(string from, string to)
        {
            if (!DateTime.TryParse(from, out var fromDate)) fromDate = DateTime.Today.AddDays(-6);
            if (!DateTime.TryParse(to, out var toDate)) toDate = DateTime.Today;

            fromDate = fromDate.Date;
            toDate = toDate.Date;

            var dayCount = (int)(toDate - fromDate).TotalDays + 1;
            if (dayCount < 1) dayCount = 1;
            if (dayCount > 180) dayCount = 180;

            var rangeEnd = toDate.AddDays(1);

            var createdDates = await _context.TaskItems
                .Where(t => t.CreatedAt >= fromDate && t.CreatedAt < rangeEnd)
                .Select(t => t.CreatedAt.Date).ToListAsync();

            var completedDates = await _context.TaskItems
                .Where(t => t.Status == TaskItemStatus.Completed && t.UpdatedAt >= fromDate && t.UpdatedAt < rangeEnd)
                .Select(t => t.UpdatedAt.Date).ToListAsync();

            var overdueDates = await _context.TaskItems
                .Where(t => t.Status != TaskItemStatus.Completed && t.Deadline >= fromDate && t.Deadline < rangeEnd)
                .Select(t => t.Deadline.Date).ToListAsync();

            var labels = new List<string>();
            var created = new List<int>();
            var completed = new List<int>();
            var overdue = new List<int>();

            if (dayCount <= 60)
            {
                for (var d = fromDate; d <= toDate; d = d.AddDays(1))
                {
                    labels.Add(d.ToString("dd/MM"));
                    created.Add(createdDates.Count(x => x == d));
                    completed.Add(completedDates.Count(x => x == d));
                    overdue.Add(overdueDates.Count(x => x == d));
                }
            }
            else
            {
                for (var ws = fromDate; ws <= toDate; ws = ws.AddDays(7))
                {
                    var we = ws.AddDays(6); if (we > toDate) we = toDate;
                    labels.Add(ws.ToString("dd/MM"));
                    created.Add(createdDates.Count(x => x >= ws && x <= we));
                    completed.Add(completedDates.Count(x => x >= ws && x <= we));
                    overdue.Add(overdueDates.Count(x => x >= ws && x <= we));
                }
            }

            return Json(new { labels, created, completed, overdue });
        }

        [HttpGet]
        public async Task<IActionResult> OverviewStats(string from, string to)
        {
            if (!DateTime.TryParse(from, out var fromDate)) fromDate = DateTime.Today.AddDays(-6);
            if (!DateTime.TryParse(to, out var toDate)) toDate = DateTime.Today;
            fromDate = fromDate.Date;
            toDate = toDate.Date;
            var rangeEnd = toDate.AddDays(1);
            var now = DateTime.Now;

            var totalInRange = await _context.TaskItems
                .CountAsync(t => t.Deadline >= fromDate && t.Deadline < rangeEnd);
            var completedInRange = await _context.TaskItems
                .CountAsync(t => t.Status == TaskItemStatus.Completed && t.Deadline >= fromDate && t.Deadline < rangeEnd);
            var overdueInRange = await _context.TaskItems
                .CountAsync(t => t.Status != TaskItemStatus.Completed && t.Deadline >= fromDate && t.Deadline < rangeEnd && t.Deadline < now);
            var schedulesInRange = await _context.ScheduleItems
                .CountAsync(s => s.StartTime >= fromDate && s.StartTime < rangeEnd);

            int completedRate = totalInRange == 0 ? 0 : (int)Math.Round(completedInRange * 100.0 / totalInRange);

            // dayCount label for display
            var dayCount = (int)(toDate - fromDate).TotalDays + 1;
            var rangeLabel = dayCount == 1
                ? $"Ngày {fromDate:dd/MM/yyyy}"
                : $"{fromDate:dd/MM} – {toDate:dd/MM/yyyy}";

            return Json(new
            {
                tasks = totalInRange,
                overdue = overdueInRange,
                completedRate,
                schedules = schedulesInRange,
                rangeLabel,
                taskLabel = dayCount == 1 ? "Task Hôm Nay" : $"Task ({dayCount} ngày)",
                overdueLabel = dayCount == 1 ? "Task Quá Hạn" : $"Quá Hạn ({dayCount} ngày)",
                rateLabel = "Tỷ lệ hoàn thành",
                schedLabel = dayCount == 1 ? "Lịch Trình Hôm Nay" : $"Lịch ({dayCount} ngày)"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                await _userManager.AddToRoleAsync(user, "Admin");

                string? emailError = null;
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "[HUTECH Schedule] Cập nhật quyền hạn tài khoản",
                            "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                            "<h2 style='color:#2563eb;'>&#9733; Cập nhật quyền quản trị viên</h2>" +
                            $"<p>Chào <strong>{user.Email}</strong>,</p>" +
                            "<p>Tài khoản của bạn đã được <strong>gán quyền Admin</strong> bởi quản trị viên hệ thống HUTECH Schedule.</p>" +
                            "<p>Bây giờ bạn đã có quyền truy cập vào bảng điều khiển quản trị (Admin Dashboard).</p>" +
                            $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                            "</div>");
                    }
                    catch (Exception ex)
                    {
                        emailError = ex.Message;
                    }
                }

                if (emailError == null)
                {
                    TempData["AdminMessage"] = $"Đã gán quyền Admin cho {user.Email}.";
                }
                else
                {
                    TempData["AdminError"] = $"Đã gán quyền Admin cho {user.Email}, nhưng lỗi gửi email: {emailError}";
                }
            }

            return RedirectToAction(nameof(Index), new { section = "users" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Email == IdentitySeedData.AdminEmail)
            {
                TempData["AdminError"] = "Không thể hạ quyền tài khoản admin mặc định.";
                return RedirectToAction(nameof(Index), new { section = "users" });
            }

            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Count <= 1)
            {
                TempData["AdminError"] = "Hệ thống cần ít nhất một Admin.";
                return RedirectToAction(nameof(Index), new { section = "users" });
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
                if (!await _userManager.IsInRoleAsync(user, "User"))
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }

                string? emailError = null;
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "[HUTECH Schedule] Cập nhật quyền hạn tài khoản",
                            "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                            "<h2 style='color:#4b5563;'>&#9670; Cập nhật quyền tài khoản</h2>" +
                            $"<p>Chào <strong>{user.Email}</strong>,</p>" +
                            "<p>Tài khoản của bạn đã được quản trị viên <strong>hạ quyền từ Admin về User thông thường</strong> trên hệ thống HUTECH Schedule.</p>" +
                            "<p>Bạn không còn quyền truy cập vào bảng điều khiển quản trị.</p>" +
                            $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                            "</div>");
                    }
                    catch (Exception ex)
                    {
                        emailError = ex.Message;
                    }
                }

                if (emailError == null)
                {
                    TempData["AdminMessage"] = $"Đã hạ {user.Email} về quyền User.";
                }
                else
                {
                    TempData["AdminError"] = $"Đã hạ {user.Email} về quyền User, nhưng lỗi gửi email: {emailError}";
                }
            }

            return RedirectToAction(nameof(Index), new { section = "users" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && user.Email != IdentitySeedData.AdminEmail)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

                string? emailError = null;
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "[HUTECH Schedule] Tài khoản bị khóa",
                            "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                            "<h2 style='color:#dc2626;'>&#128274; Tài khoản đã bị khóa</h2>" +
                            $"<p>Tài khoản <strong>{user.Email}</strong> đã bị khóa bởi quản trị viên hệ thống HUTECH Schedule.</p>" +
                            "<div style='background:#fef2f2;border-left:4px solid #dc2626;padding:16px;border-radius:6px;margin:16px 0;'>" +
                            "<p style='margin:0;'><strong>Lý do khóa:</strong></p>" +
                            "<p style='margin:8px 0 0;'>Tài khoản bị khóa trực tiếp bởi quản trị viên.</p>" +
                            "</div>" +
                            "<p>Tài khoản của bạn đã bị tạm khóa. Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.</p>" +
                            $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                            "</div>");
                    }
                    catch (Exception ex)
                    {
                        emailError = ex.Message;
                    }
                }

                if (emailError == null)
                {
                    TempData["AdminMessage"] = $"Đã khóa tài khoản {user.Email}.";
                }
                else
                {
                    TempData["AdminError"] = $"Đã khóa tài khoản {user.Email}, nhưng lỗi gửi email: {emailError}";
                }
            }
            else
            {
                TempData["AdminError"] = "Không thể khóa tài khoản admin mặc định.";
            }

            return RedirectToAction(nameof(Index), new { section = "users" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);

                string? emailError = null;
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "[HUTECH Schedule] Tài khoản đã được mở khóa",
                            "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                            "<h2 style='color:#16a34a;'>&#128275; Tài khoản đã được mở khóa</h2>" +
                            $"<p>Chào <strong>{user.Email}</strong>,</p>" +
                            "<p>Tài khoản của bạn đã được <strong>mở khóa thành công</strong> bởi quản trị viên hệ thống HUTECH Schedule.</p>" +
                            "<p>Bây giờ bạn có thể đăng nhập lại và sử dụng các tính năng bình thường.</p>" +
                            $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                            "</div>");
                    }
                    catch (Exception ex)
                    {
                        emailError = ex.Message;
                    }
                }

                if (emailError == null)
                {
                    TempData["AdminMessage"] = $"Đã mở khóa tài khoản {user.Email}.";
                }
                else
                {
                    TempData["AdminError"] = $"Đã mở khóa tài khoản {user.Email}, nhưng lỗi gửi email: {emailError}";
                }
            }

            return RedirectToAction(nameof(Index), new { section = "users" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return RedirectToAction(nameof(Index), new { section = "users" });
            }

            if (user.Email == IdentitySeedData.AdminEmail || user.Id == _userManager.GetUserId(User))
            {
                TempData["AdminError"] = "Không thể xóa tài khoản admin mặc định hoặc chính tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index), new { section = "users" });
            }

            string? emailError = null;
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "[HUTECH Schedule] Tài khoản của bạn đã bị xóa vĩnh viễn",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#dc2626;'>&#10060; Tài khoản bị xóa vĩnh viễn</h2>" +
                        $"<p>Chào bạn,</p>" +
                        $"<p>Tài khoản của bạn (<strong>{user.Email}</strong>) đã bị <strong>xóa vĩnh viễn</strong> khỏi hệ thống HUTECH Schedule theo quyết định của quản trị viên.</p>" +
                        "<p>Tất cả lịch trình, task vụ và thông tin cá nhân liên quan đều đã bị gỡ bỏ khỏi hệ thống.</p>" +
                        $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "</div>");
                }
                catch (Exception ex)
                {
                    emailError = ex.Message;
                }
            }

            var schedules = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);
            _context.ScheduleItems.RemoveRange(schedules);
            await _context.SaveChangesAsync();

            await _userManager.DeleteAsync(user);

            if (emailError == null)
            {
                TempData["AdminMessage"] = $"Đã xóa tài khoản {user.Email}.";
            }
            else
            {
                TempData["AdminError"] = $"Đã xóa tài khoản {user.Email}, nhưng lỗi gửi email: {emailError}";
            }

            return RedirectToAction(nameof(Index), new { section = "users" });
        }

        private static string NormalizeSection(string? section)
        {
            return section?.ToLowerInvariant() switch
            {
                "users" => "users",
                "schedules" => "schedules",
                "tasks" => "tasks",
                "activity" => "activity",
                "notifications" => "notifications",
                "reports" => "reports",
                "settings" => "settings",
                _ => "overview"
            };
        }

        private static string BuildStatusLabel(string status)
        {
            return status switch
            {
                nameof(TaskItemStatus.NotStarted) => "Chưa bắt đầu",
                nameof(TaskItemStatus.InProgress) => "Đang làm",
                nameof(TaskItemStatus.Completed) => "Hoàn thành",
                nameof(TaskItemStatus.Overdue) => "Quá hạn",
                _ => status
            };
        }

        private static string BuildPriorityLabel(string priority)
        {
            return priority switch
            {
                nameof(TaskPriorityLevel.Low) => "Thấp",
                nameof(TaskPriorityLevel.Medium) => "Trung bình",
                nameof(TaskPriorityLevel.High) => "Cao",
                nameof(TaskPriorityLevel.Urgent) => "Khẩn cấp",
                _ => priority
            };
        }

        // ── Report Actions ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WarnUser(int reportId, string adminNote)
        {
            var report = await _context.UserReports.FindAsync(reportId);
            if (report == null) return NotFound();

            report.Status = ReportStatus.Warned;
            report.AdminNote = adminNote;
            report.HandledAt = DateTime.Now;
            await _context.SaveChangesAsync();

            string? emailError = null;
            // Send warning email to reported user
            var reportedUser = await _userManager.FindByIdAsync(report.ReportedUserId);
            if (reportedUser?.Email != null)
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        reportedUser.Email,
                        "[HUTECH Schedule] Cảnh báo tài khoản",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#d97706;'>&#9888; Cảnh báo từ quản trị viên</h2>" +
                        $"<p>Tài khoản của bạn (<strong>{reportedUser.Email}</strong>) đã nhận được cảnh báo từ đội quản trị hệ thống HUTECH Schedule.</p>" +
                        "<div style='background:#fffbeb;border-left:4px solid #d97706;padding:16px;border-radius:6px;margin:16px 0;'>" +
                        "<p style='margin:0;'><strong>Lý do cảnh báo:</strong></p>" +
                        $"<p style='margin:8px 0 0;'>{System.Net.WebUtility.HtmlEncode(adminNote ?? report.Reason)}</p>" +
                        "</div>" +
                        "<p>Đây là cảnh báo lần đầu. Nếu hành vi tiếp tục, tài khoản của bạn có thể bị khóa vĩnh viễn.</p>" +
                        "<p>Nếu bạn cho rằng đây là sai sót, hãy liên hệ với quản trị viên.</p>" +
                        $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "</div>");
                }
                catch (Exception ex)
                {
                    emailError = ex.Message;
                }
            }

            if (emailError == null)
            {
                TempData["AdminMessage"] = $"Đã gửi cảnh báo đến {reportedUser?.Email ?? report.ReportedUserId}.";
            }
            else
            {
                TempData["AdminError"] = $"Đã gửi cảnh báo đến {reportedUser?.Email ?? report.ReportedUserId}, nhưng lỗi gửi email: {emailError}";
            }
            return RedirectToAction(nameof(Index), new { section = "notifications" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockUserFromReport(int reportId, string adminNote)
        {
            var report = await _context.UserReports.FindAsync(reportId);
            if (report == null) return NotFound();

            var reportedUser = await _userManager.FindByIdAsync(report.ReportedUserId);
            string? emailError = null;
            if (reportedUser != null)
            {
                // Lock for 100 years = permanent
                await _userManager.SetLockoutEnabledAsync(reportedUser, true);
                await _userManager.SetLockoutEndDateAsync(reportedUser, DateTimeOffset.UtcNow.AddYears(100));

                // Send lock notification email
                if (!string.IsNullOrWhiteSpace(reportedUser.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            reportedUser.Email,
                            "[HUTECH Schedule] Tài khoản bị khóa",
                            "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                            "<h2 style='color:#dc2626;'>&#128274; Tài khoản đã bị khóa</h2>" +
                            $"<p>Tài khoản <strong>{reportedUser.Email}</strong> đã bị khóa bởi quản trị viên hệ thống HUTECH Schedule.</p>" +
                            "<div style='background:#fef2f2;border-left:4px solid #dc2626;padding:16px;border-radius:6px;margin:16px 0;'>" +
                            "<p style='margin:0;'><strong>Lý do khóa:</strong></p>" +
                            $"<p style='margin:8px 0 0;'>{System.Net.WebUtility.HtmlEncode(adminNote ?? report.Reason)}</p>" +
                            "</div>" +
                            "<p>Tài khoản của bạn đã bị tạm khóa. Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.</p>" +
                            $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                            "</div>");
                    }
                    catch (Exception ex)
                    {
                        emailError = ex.Message;
                    }
                }
            }

            report.Status = ReportStatus.Locked;
            report.AdminNote = adminNote;
            report.HandledAt = DateTime.Now;
            await _context.SaveChangesAsync();

            if (emailError == null)
            {
                TempData["AdminMessage"] = $"Đã khóa tài khoản {reportedUser?.Email ?? report.ReportedUserId} và gửi thông báo email.";
            }
            else
            {
                TempData["AdminError"] = $"Đã khóa tài khoản {reportedUser?.Email ?? report.ReportedUserId}, nhưng lỗi gửi email: {emailError}";
            }
            return RedirectToAction(nameof(Index), new { section = "notifications" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissReport(int reportId)
        {
            var report = await _context.UserReports.FindAsync(reportId);
            if (report == null) return NotFound();

            report.Status = ReportStatus.Dismissed;
            report.HandledAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["AdminMessage"] = "Đã bỏ qua báo cáo này.";
            return RedirectToAction(nameof(Index), new { section = "notifications" });
        }
        [HttpGet]
        public async Task<IActionResult> GetUserEmail(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user == null ? NotFound() : Content(user.Email ?? userId);
        }
    }
}
