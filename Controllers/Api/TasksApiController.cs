using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.DTOs;
using schedule.Helpers;
using schedule.Models;

namespace schedule.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/tasks")]
    public class TasksApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public TasksApiController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<TaskItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTasks([FromQuery] string? statusFilter, [FromQuery] string? dateFilter)
        {
            var currentUserId = _userManager.GetUserId(User);
            var query = _context.TaskItems
                .Include(t => t.ScheduleItem)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(t => t.CreatedByUserId == currentUserId);
            }

            // Apply Status Filter
            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<TaskItemStatus>(statusFilter, true, out var status))
                {
                    query = query.Where(t => t.Status == status);
                }
            }

            // Apply Date Filter
            if (!string.IsNullOrWhiteSpace(dateFilter) && !dateFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var today = DateTime.Today;
                if (dateFilter.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(t => t.Deadline.Date == today);
                }
                else if (dateFilter.Equals("overdue", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(t => t.Deadline < DateTime.Now && t.Status != TaskItemStatus.Completed);
                }
                else if (dateFilter.Equals("upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(t => t.Deadline >= DateTime.Now);
                }
            }

            var items = await query
                .OrderBy(t => t.Status == TaskItemStatus.Completed)
                .ThenBy(t => t.Deadline)
                .ToListAsync();

            var dtos = items.Select(MapToDto);
            return Ok(dtos);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetTask(int id)
        {
            var item = await _context.TaskItems
                .Include(t => t.ScheduleItem)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            return Ok(MapToDto(item));
        }

        [HttpPost]
        [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateTask([FromBody] TaskItemCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var schedule = await _context.ScheduleItems.FindAsync(dto.ScheduleItemId);
            if (schedule == null)
            {
                return BadRequest("Lịch trình đính kèm không tồn tại.");
            }

            if (!CanManageSchedule(schedule))
            {
                return Forbid();
            }

            var user = await _userManager.GetUserAsync(User);
            var item = new TaskItem
            {
                ScheduleItemId = dto.ScheduleItemId,
                Title = dto.Title,
                Description = dto.Description,
                Deadline = dto.Deadline,
                Status = dto.Status,
                Priority = dto.Priority,
                Color = dto.Color,
                AttachmentUrl = dto.AttachmentUrl,
                CreatedByUserId = user?.Id,
                CreatedByEmail = user?.Email ?? User.Identity?.Name,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.TaskItems.Add(item);
            await _context.SaveChangesAsync();

            // Load navigation property
            item.ScheduleItem = schedule;

            return CreatedAtAction(nameof(GetTask), new { id = item.Id }, MapToDto(item));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskItemCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var item = await _context.TaskItems
                .Include(t => t.ScheduleItem)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            var schedule = await _context.ScheduleItems.FindAsync(dto.ScheduleItemId);
            if (schedule == null)
            {
                return BadRequest("Lịch trình đính kèm không tồn tại.");
            }

            if (!CanManageSchedule(schedule))
            {
                return Forbid();
            }

            item.ScheduleItemId = dto.ScheduleItemId;
            item.Title = dto.Title;
            item.Description = dto.Description;
            item.Deadline = dto.Deadline;
            item.Status = dto.Status;
            item.Priority = dto.Priority;
            item.Color = dto.Color;
            item.AttachmentUrl = dto.AttachmentUrl;
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(item));
        }

        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(typeof(TaskItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateTaskStatus(int id, [FromBody] TaskItemUpdateStatusDto dto)
        {
            var item = await _context.TaskItems
                .Include(t => t.ScheduleItem)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            item.Status = dto.Status;
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(item));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var item = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            _context.TaskItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CanManage(TaskItem item)
        {
            return User.IsInRole("Admin") || item.CreatedByUserId == _userManager.GetUserId(User);
        }

        private bool CanManageSchedule(ScheduleItem schedule)
        {
            return User.IsInRole("Admin") || schedule.CreatedByUserId == _userManager.GetUserId(User);
        }

        private static TaskItemDto MapToDto(TaskItem t)
        {
            return new TaskItemDto
            {
                Id = t.Id,
                ScheduleItemId = t.ScheduleItemId,
                ScheduleItemTitle = t.ScheduleItem?.Title,
                Title = t.Title,
                Description = t.Description,
                Deadline = t.Deadline,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                Color = t.Color,
                AttachmentUrl = t.AttachmentUrl,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByEmail = t.CreatedByEmail,
                CreatedAt = t.CreatedAt
            };
        }
    }
}
