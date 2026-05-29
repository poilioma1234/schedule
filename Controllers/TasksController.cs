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
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public TasksController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ScheduleItemId,Title,Description,Deadline,Status,Priority,Color,AttachmentUrl")] TaskItem task)
        {
            task.Id = 0;
            var schedule = await _context.ScheduleItems.FindAsync(task.ScheduleItemId);
            if (schedule == null)
            {
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
                return RedirectToAction("Edit", "Schedule", new { id = task.ScheduleItemId });
            }

            var user = await _userManager.GetUserAsync(User);
            task.CreatedByUserId = schedule.CreatedByUserId ?? user?.Id;
            task.CreatedByEmail = schedule.CreatedByEmail ?? user?.Email ?? User.Identity?.Name;
            task.CreatedAt = DateTime.Now;
            task.UpdatedAt = DateTime.Now;

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm task vào lịch.";
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
