using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public SearchController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var model = new SearchViewModel
            {
                Query = q?.Trim() ?? string.Empty,
                Results = await BuildResultsAsync(q, 40)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Live(string? q)
        {
            var results = await BuildResultsAsync(q, 8);
            return Json(results);
        }

        private async Task<List<SearchResultViewModel>> BuildResultsAsync(string? q, int take)
        {
            var keyword = q?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<SearchResultViewModel>();
            }

            var currentUserId = _userManager.GetUserId(User);
            var canSearchAll = User.IsInRole("Admin");

            IQueryable<ScheduleItem> scheduleQuery = _context.ScheduleItems.AsNoTracking();
            IQueryable<TaskItem> taskQuery = _context.TaskItems.AsNoTracking().Include(task => task.ScheduleItem);

            if (!canSearchAll)
            {
                scheduleQuery = scheduleQuery.Where(item => item.CreatedByUserId == currentUserId);
                taskQuery = taskQuery.Where(item => item.CreatedByUserId == currentUserId);
            }

            var schedules = await scheduleQuery
                .Where(item =>
                    item.Title.ToLower().Contains(keyword)
                    || (item.Description != null && item.Description.ToLower().Contains(keyword))
                    || (item.Location != null && item.Location.ToLower().Contains(keyword))
                    || (item.CreatedByEmail != null && item.CreatedByEmail.ToLower().Contains(keyword)))
                .OrderBy(item => item.StartTime)
                .Take(take)
                .Select(item => new SearchResultViewModel
                {
                    Title = item.Title,
                    Detail = $"{item.StartTime:dd/MM/yyyy HH:mm} - {item.EndTime:HH:mm} · {item.CreatedByEmail}",
                    Type = "Lịch trình",
                    Url = Url.Action("Edit", "Schedule", new { id = item.Id }) ?? $"/Schedule/Edit/{item.Id}"
                })
                .ToListAsync();

            var tasks = await taskQuery
                .Where(item =>
                    item.Title.ToLower().Contains(keyword)
                    || (item.Description != null && item.Description.ToLower().Contains(keyword))
                    || (item.CreatedByEmail != null && item.CreatedByEmail.ToLower().Contains(keyword))
                    || (item.ScheduleItem != null && item.ScheduleItem.Title.ToLower().Contains(keyword)))
                .OrderBy(item => item.Deadline)
                .Take(take)
                .Select(item => new SearchResultViewModel
                {
                    Title = item.Title,
                    Detail = $"{item.Deadline:dd/MM/yyyy HH:mm} · {(item.ScheduleItem != null ? item.ScheduleItem.Title : "Không rõ lịch")}",
                    Type = "Task",
                    Url = Url.Action("Edit", "Tasks", new { id = item.Id }) ?? $"/Tasks/Edit/{item.Id}"
                })
                .ToListAsync();

            var profileResults = canSearchAll
                ? await _context.UserProfiles
                    .AsNoTracking()
                    .Where(profile =>
                        profile.DisplayName.ToLower().Contains(keyword)
                        || profile.UserId.ToLower().Contains(keyword))
                    .Take(take)
                    .Select(profile => new SearchResultViewModel
                    {
                        Title = profile.DisplayName,
                        Detail = profile.IsProfilePublic ? "Profile công khai" : "Profile riêng tư",
                        Type = "Người dùng",
                        Url = Url.Action("Details", "Profile", new { id = profile.UserId }) ?? $"/Profile/{profile.UserId}"
                    })
                    .ToListAsync()
                : new List<SearchResultViewModel>();

            return schedules
                .Concat(tasks)
                .Concat(profileResults)
                .Take(take)
                .ToList();
        }
    }
}
