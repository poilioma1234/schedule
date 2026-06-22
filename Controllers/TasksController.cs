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
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public TasksController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? searchString, string statusFilter = "all", string dateFilter = "all")
        {
            var currentUserId = _userManager.GetUserId(User);
            var query = _context.TaskItems
                .Include(task => task.ScheduleItem)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(task => task.CreatedByUserId == currentUserId);
            }

            // Always fetch all tasks to support instant zero-reload client-side filtering via DataTables
            var allTasks = await query
                .OrderBy(task => task.Status == TaskItemStatus.Completed)
                .ThenBy(task => task.Deadline)
                .ToListAsync();

            ViewBag.SearchString = searchString ?? string.Empty;
            ViewBag.StatusFilter = NormalizeStatusFilter(statusFilter);
            ViewBag.DateFilter = NormalizeDateFilter(dateFilter);
            ViewBag.TotalTasks = allTasks.Count;

            return View(allTasks);
        }

        [HttpGet]
        public async Task<IActionResult> Create(DateTime? date)
        {
            var currentUserId = _userManager.GetUserId(User);
            var today = date?.Date ?? DateTime.Today;

            var schedulesQuery = _context.ScheduleItems.AsQueryable();
            if (!User.IsInRole("Admin"))
            {
                schedulesQuery = schedulesQuery.Where(s => s.CreatedByUserId == currentUserId);
            }
            var schedules = await schedulesQuery.ToListAsync();

            ViewBag.Schedules = schedules;

            return View(new TaskItem
            {
                Deadline = today.AddHours(17),
                Priority = TaskPriorityLevel.Medium,
                Status = TaskItemStatus.NotStarted,
                Color = TaskDisplayHelper.PriorityColor(TaskPriorityLevel.Medium)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ScheduleItemId,Title,Description,Deadline,Status,Priority,Color,AttachmentUrl")] TaskItem task, string? returnUrl = null)
        {
            task.Id = 0;
            var schedule = await _context.ScheduleItems.FindAsync(task.ScheduleItemId);
            if (schedule == null)
            {
                if (returnUrl == "tasks-list")
                {
                    TempData["TaskError"] = "Vui lòng chọn một lịch trình phù hợp.";
                    var currentUserId = _userManager.GetUserId(User);
                    var schedulesQuery = _context.ScheduleItems.AsQueryable();
                    if (!User.IsInRole("Admin"))
                    {
                        schedulesQuery = schedulesQuery.Where(s => s.CreatedByUserId == currentUserId);
                    }
                    ViewBag.Schedules = await schedulesQuery.ToListAsync();
                    return View("Create", task);
                }
                return NotFound();
            }

            if (!CanManage(schedule))
            {
                return Forbid();
            }

            NormalizeTask(task);

            if (!ModelState.IsValid)
            {
                TempData["TaskError"] = "Task chưa hợp lệ. Vui lòng kiểm tra tiêu đề, deadline và link đính kèm.";
                if (returnUrl == "tasks-list")
                {
                    var currentUserId = _userManager.GetUserId(User);
                    var schedulesQuery = _context.ScheduleItems.AsQueryable();
                    if (!User.IsInRole("Admin"))
                    {
                        schedulesQuery = schedulesQuery.Where(s => s.CreatedByUserId == currentUserId);
                    }
                    ViewBag.Schedules = await schedulesQuery.ToListAsync();
                    return View("Create", task);
                }
                return RedirectToAction("Edit", "Schedule", new { id = task.ScheduleItemId });
            }

            var user = await _userManager.GetUserAsync(User);
            task.CreatedByUserId = schedule.CreatedByUserId ?? user?.Id;
            task.CreatedByEmail = schedule.CreatedByEmail ?? user?.Email ?? User.Identity?.Name;
            task.CreatedAt = DateTime.Now;
            task.UpdatedAt = DateTime.Now;

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm task mới.";
            if (returnUrl == "tasks-list")
            {
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction("Edit", "Schedule", new { id = task.ScheduleItemId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var task = await _context.TaskItems
                .Include(item => item.ScheduleItem)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            if (task.ScheduleItem == null || !CanManage(task.ScheduleItem))
            {
                return Forbid();
            }

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Title,Description,Deadline,Status,Priority,Color,AttachmentUrl")] TaskItem task)
        {
            var existingTask = await _context.TaskItems
                .Include(item => item.ScheduleItem)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (existingTask == null)
            {
                return NotFound();
            }

            if (existingTask.ScheduleItem == null || !CanManage(existingTask.ScheduleItem))
            {
                return Forbid();
            }

            task.ScheduleItemId = existingTask.ScheduleItemId;
            NormalizeTask(task);

            if (!ModelState.IsValid)
            {
                task.ScheduleItem = existingTask.ScheduleItem;
                return View(task);
            }

            existingTask.Title = task.Title.Trim();
            existingTask.Description = task.Description?.Trim();
            existingTask.Deadline = task.Deadline;
            existingTask.Status = task.Status;
            existingTask.Priority = task.Priority;
            existingTask.Color = task.Color;
            existingTask.AttachmentUrl = task.AttachmentUrl?.Trim();
            existingTask.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật task.";
            return RedirectToAction("Edit", "Schedule", new { id = existingTask.ScheduleItemId });
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.TaskItems
                .Include(item => item.ScheduleItem)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            if (task.ScheduleItem == null || !CanManage(task.ScheduleItem))
            {
                return Forbid();
            }

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var task = await _context.TaskItems
                .Include(item => item.ScheduleItem)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            if (task.ScheduleItem == null || !CanManage(task.ScheduleItem))
            {
                return Forbid();
            }

            var scheduleId = task.ScheduleItemId;
            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa task.";
            return RedirectToAction("Edit", "Schedule", new { id = scheduleId });
        }

        private bool CanManage(ScheduleItem schedule)
        {
            return User.IsInRole("Admin") || schedule.CreatedByUserId == _userManager.GetUserId(User);
        }

        private static string NormalizeStatusFilter(string statusFilter)
        {
            return statusFilter.ToLowerInvariant() switch
            {
                "today" => "today",
                "overdue" => "overdue",
                "completed" => "completed",
                "open" => "open",
                _ => "all"
            };
        }

        private static string NormalizeDateFilter(string dateFilter)
        {
            return dateFilter.ToLowerInvariant() switch
            {
                "today" => "today",
                "7days" => "7days",
                "14days" => "14days",
                "30days" => "30days",
                _ => "all"
            };
        }

        private static void NormalizeTask(TaskItem task)
        {
            task.Title = task.Title.Trim();
            task.Description = task.Description?.Trim();
            task.AttachmentUrl = task.AttachmentUrl?.Trim();

            if (string.IsNullOrWhiteSpace(task.Color))
            {
                task.Color = TaskDisplayHelper.PriorityColor(task.Priority);
            }
        }
    }
}
