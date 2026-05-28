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

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim().ToLower();
                query = query.Where(item => item.Title.ToLower().Contains(keyword));
            }

            if (startDate.HasValue)
            {
                query = query.Where(item => item.StartTime.Date == startDate.Value.Date);
            }

            ViewBag.SearchString = searchString;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.ViewingUserId = userId;
            ViewBag.ViewingUserEmail = await GetViewingUserEmailAsync(userId);

            var items = await query
                .OrderBy(item => item.StartTime)
                .ToListAsync();

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
            var events = await BuildUserScheduleQuery(userId)
                .Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    start = item.StartTime.ToString("s"),
                    end = item.EndTime.ToString("s"),
                    color = item.IsImportant ? "#dc3545" : "#0d6efd"
                })
                .ToListAsync();

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

        private void ValidateScheduleTime(ScheduleItem item)
        {
            if (item.EndTime <= item.StartTime)
            {
                ModelState.AddModelError(nameof(ScheduleItem.EndTime), "Thời gian kết thúc phải sau thời gian bắt đầu.");
            }
        }
    }
}
