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
using schedule.Models;

namespace schedule.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/schedules")]
    public class SchedulesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public SchedulesApiController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ScheduleItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSchedules([FromQuery] string? searchString, [FromQuery] DateTime? startDate)
        {
            var query = BuildUserScheduleQuery();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(s => s.Title.Contains(searchString) || (s.Description != null && s.Description.Contains(searchString)));
            }

            if (startDate.HasValue)
            {
                var date = startDate.Value.Date;
                query = query.Where(s => s.StartTime >= date && s.StartTime < date.AddDays(1));
            }

            var items = await query
                .Include(s => s.Tasks)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            var dtos = items.Select(MapToDto);
            return Ok(dtos);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ScheduleItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSchedule(int id)
        {
            var item = await _context.ScheduleItems
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == id);

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
        [ProducesResponseType(typeof(ScheduleItemDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSchedule([FromBody] ScheduleItemCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.EndTime <= dto.StartTime)
            {
                return BadRequest("Thời gian kết thúc phải sau thời gian bắt đầu.");
            }

            var user = await _userManager.GetUserAsync(User);
            var item = new ScheduleItem
            {
                Title = dto.Title,
                Description = dto.Description,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Location = dto.Location,
                IsImportant = dto.IsImportant,
                ReceiverEmail = dto.ReceiverEmail,
                ReminderMinutes = dto.ReminderMinutes,
                CreatedByUserId = user?.Id,
                CreatedByEmail = user?.Email ?? User.Identity?.Name,
                CreatedAt = DateTime.Now
            };

            _context.ScheduleItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSchedule), new { id = item.Id }, MapToDto(item));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ScheduleItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] ScheduleItemCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (dto.EndTime <= dto.StartTime)
            {
                return BadRequest("Thời gian kết thúc phải sau thời gian bắt đầu.");
            }

            var item = await _context.ScheduleItems.FirstOrDefaultAsync(s => s.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            if (!CanManage(item))
            {
                return Forbid();
            }

            item.Title = dto.Title;
            item.Description = dto.Description;
            item.StartTime = dto.StartTime;
            item.EndTime = dto.EndTime;
            item.Location = dto.Location;
            item.IsImportant = dto.IsImportant;
            item.ReceiverEmail = dto.ReceiverEmail;
            item.ReminderMinutes = dto.ReminderMinutes;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(item));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var item = await _context.ScheduleItems.FirstOrDefaultAsync(s => s.Id == id);
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

            return NoContent();
        }

        [HttpGet("events")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCalendarEvents([FromQuery] string? userId)
        {
            var query = _context.ScheduleItems.AsQueryable();

            if (User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(item => item.CreatedByUserId == userId);
            }
            else if (!User.IsInRole("Admin"))
            {
                var currentUserId = _userManager.GetUserId(User);
                query = query.Where(item => item.CreatedByUserId == currentUserId);
            }

            var schedules = await query
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

                var startStr = item.start;
                var endStr = item.end;

                if (DateTime.TryParse(startStr, out DateTime startDt) && DateTime.TryParse(endStr, out DateTime endDt))
                {
                    if (endDt <= startDt)
                    {
                        endStr = startDt.AddMinutes(30).ToString("s");
                    }
                    else if ((endDt - startDt).TotalMinutes < 30)
                    {
                        endStr = startDt.AddMinutes(30).ToString("s");
                    }
                }

                return new
                {
                    item.id,
                    item.title,
                    start = startStr,
                    end = endStr,
                    color = highestPriority != null
                        ? highestPriority.Color
                        : item.isImportant ? "#dc3545" : "#0d6efd",
                    extendedProps = new
                    {
                        tasks = item.tasks.ToList()
                    }
                };
            });

            return Ok(events);
        }

        private IQueryable<ScheduleItem> BuildUserScheduleQuery()
        {
            var query = _context.ScheduleItems.AsQueryable();

            if (User.IsInRole("Admin"))
            {
                return query;
            }

            var currentUserId = _userManager.GetUserId(User);
            return query.Where(item => item.CreatedByUserId == currentUserId);
        }

        private bool CanManage(ScheduleItem item)
        {
            return User.IsInRole("Admin") || item.CreatedByUserId == _userManager.GetUserId(User);
        }

        private static ScheduleItemDto MapToDto(ScheduleItem item)
        {
            return new ScheduleItemDto
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                Location = item.Location,
                IsImportant = item.IsImportant,
                ReceiverEmail = item.ReceiverEmail,
                ReminderMinutes = item.ReminderMinutes,
                CreatedByUserId = item.CreatedByUserId,
                CreatedByEmail = item.CreatedByEmail,
                CreatedAt = item.CreatedAt,
                Tasks = item.Tasks.Select(t => new TaskItemDto
                {
                    Id = t.Id,
                    ScheduleItemId = t.ScheduleItemId,
                    ScheduleItemTitle = item.Title,
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
                }).ToList()
            };
        }
    }
}
