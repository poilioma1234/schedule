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
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ScheduleController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? searchString, DateTime? startDate, string? userId)
        {
            var query = BuildUserScheduleQuery(userId);

            var items = await query
                .Include(item => item.Tasks)
                .OrderByDescending(item => item.StartTime)
                .ToListAsync();

            ViewBag.SearchString = searchString ?? string.Empty;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ViewBag.ViewingUserId = userId;
            ViewBag.ViewingUserEmail = await GetViewingUserEmailAsync(userId);

            return View(items);
        }

        [HttpGet]
        public IActionResult Create(DateTime? date)
        {
            var start = date?.Date.AddHours(8) ?? DateTime.Today.AddHours(8);
            return View(new ScheduleItem
            {
                StartTime = start,
                EndTime = start.AddHours(1),
                ReminderMinutes = 5
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleItem item)
        {
            ValidateScheduleTime(item);

            if (!ModelState.IsValid)
            {
                return View(item);
            }

            var user = await _userManager.GetUserAsync(User);
            item.CreatedByUserId = user?.Id;
            item.CreatedByEmail = user?.Email ?? User.Identity?.Name;
            item.CreatedAt = DateTime.Now;

            _context.ScheduleItems.Add(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm lịch mới.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.ScheduleItems
                .Include(schedule => schedule.Tasks.OrderBy(task => task.Deadline))
                .FirstOrDefaultAsync(schedule => schedule.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            if (!User.IsInRole("Admin") && !CanEditToday(item))
            {
                TempData["SuccessMessage"] = "Chỉ có lịch trong ngày hiện tại mới được sửa.";
                return RedirectToAction(nameof(Index));
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ScheduleItem item)
        {
            if (id != item.Id)
            {
                return NotFound();
            }

            ValidateScheduleTime(item);

            if (!ModelState.IsValid)
            {
                return View(item);
            }

            var existingItem = await _context.ScheduleItems.FindAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!CanManage(existingItem))
            {
                return Forbid();
            }

            if (!User.IsInRole("Admin") && !CanEditToday(existingItem))
            {
                TempData["SuccessMessage"] = "Chỉ có lịch trong ngày hiện tại mới được sửa.";
                return RedirectToAction(nameof(Index));
            }

            var timeChanged = existingItem.StartTime != item.StartTime;
            existingItem.Title = item.Title;
            existingItem.Description = item.Description;
            existingItem.StartTime = item.StartTime;
            existingItem.EndTime = item.EndTime;
            existingItem.Location = item.Location;
            existingItem.IsImportant = item.IsImportant;
            existingItem.ReceiverEmail = item.ReceiverEmail;
            existingItem.ReminderMinutes = item.ReminderMinutes;

            if (timeChanged)
            {
                existingItem.ReminderSentAt = null;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật lịch.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.ScheduleItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.ScheduleItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            _context.ScheduleItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa lịch.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Calendar(string? userId)
        {
            ViewBag.ViewingUserId = userId;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents(string? userId)
        {
            if (User.IsInRole("Admin") && string.IsNullOrWhiteSpace(userId))
            {
                var items = await _context.ScheduleItems
                    .Select(item => new
                    {
                        item.StartTime,
                        item.CreatedByEmail,
                        item.CreatedByUserId
                    })
                    .ToListAsync();

                var adminEvents = items
                    .GroupBy(item => item.StartTime.Date)
                    .Select(dayGroup =>
                    {
                        var users = dayGroup
                            .GroupBy(item => new
                            {
                                UserEmail = item.CreatedByEmail ?? "Không rõ user",
                                UserId = item.CreatedByUserId
                            })
                            .Select(userGroup => new
                            {
                                email = userGroup.Key.UserEmail,
                                userId = userGroup.Key.UserId,
                                count = userGroup.Count(),
                                url = Url.Action("Index", "Schedule", new
                                {
                                    userId = userGroup.Key.UserId,
                                    startDate = dayGroup.Key.ToString("yyyy-MM-dd")
                                })
                            })
                            .OrderBy(user => user.email)
                            .ToList();

                        return new
                        {
                            id = $"summary-{dayGroup.Key:yyyyMMdd}",
                            title = $"Menu · {users.Count} user",
                            start = dayGroup.Key.ToString("yyyy-MM-dd"),
                            allDay = true,
                            color = "#0f766e",
                            editable = false,
                            extendedProps = new
                            {
                                date = dayGroup.Key.ToString("dd/MM/yyyy"),
                                totalSchedules = dayGroup.Count(),
                                users
                            }
                        };
                    })
                    .ToList();

                return Json(adminEvents);
            }

            var schedules = await BuildUserScheduleQuery(userId)
                .Include(item => item.Tasks)
                .Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    start = item.StartTime.ToString("s"),
                    end = item.EndTime.ToString("s"),
                    isImportant = item.IsImportant,
                    tasks = item.Tasks.Select(task => new
                    {
                        task.Title,
                        task.Priority,
                        task.Color
                    })
                })
                .ToListAsync();

            var events = schedules.Select(item =>
            {
                var highestPriority = item.tasks
                    .OrderByDescending(task => task.Priority)
                    .FirstOrDefault();

                return new
                {
                    item.id,
                    item.title,
                    item.start,
                    item.end,
                    color = highestPriority != null
                        ? highestPriority.Color
                        : item.isImportant ? "#dc3545" : "#0d6efd",
                    extendedProps = new
                    {
                        tasks = item.tasks.ToList()
                    }
                };
            });

            return Json(events);
        }

        public async Task<IActionResult> ExportPdf(string? userId)
        {
            var items = await BuildUserScheduleQuery(userId)
                .OrderBy(item => item.StartTime)
                .ToListAsync();

            var ownerEmail = await GetViewingUserEmailAsync(userId) ?? User.Identity?.Name ?? "user";
            var pdf = SchedulePdfGenerator.Generate(items, ownerEmail);

            return File(pdf, "application/pdf", $"Schedule_{ownerEmail}.pdf");
        }

        private IQueryable<ScheduleItem> BuildUserScheduleQuery(string? userId)
        {
            var query = _context.ScheduleItems.AsQueryable();

            if (User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(userId))
            {
                return query.Where(item => item.CreatedByUserId == userId);
            }

            if (User.IsInRole("Admin"))
            {
                return query;
            }

            var currentUserId = _userManager.GetUserId(User);
            return query.Where(item => item.CreatedByUserId == currentUserId);
        }

        private async Task<string?> GetViewingUserEmailAsync(string? userId)
        {
            if (User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                return user?.Email;
            }

            if (User.IsInRole("Admin"))
            {
                return "Tất cả người dùng";
            }

            return User.Identity?.Name;
        }

        private bool CanManage(ScheduleItem item)
        {
            return User.IsInRole("Admin") || item.CreatedByUserId == _userManager.GetUserId(User);
        }

        private static bool CanEditToday(ScheduleItem item)
        {
            return item.StartTime.Date == DateTime.Today;
        }

        private void ValidateScheduleTime(ScheduleItem item)
        {
            if (item.EndTime <= item.StartTime)
            {
                ModelState.AddModelError(nameof(ScheduleItem.EndTime), "Thời gian kết thúc phải sau thời gian bắt đầu.");
            }
        }
    }
}
