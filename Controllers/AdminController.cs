using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? searchString, string statusFilter = "all")
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            var users = await _userManager.Users.OrderBy(user => user.Email).ToListAsync();
            var userRows = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var scheduleQuery = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);

                userRows.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? user.UserName ?? "",
                    Roles = roles.Any() ? string.Join(", ", roles) : "User",
                    IsAdmin = roles.Contains("Admin"),
                    IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                    ScheduleCount = await scheduleQuery.CountAsync(),
                    TodayScheduleCount = await scheduleQuery.CountAsync(item => item.StartTime.Date == today),
                    ActiveOrUpcomingScheduleCount = await scheduleQuery.CountAsync(item => item.EndTime >= now),
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

            var model = new AdminDashboardViewModel
            {
                TotalUsers = userRows.Count,
                ActiveUsers = userRows.Count(user => !user.IsLocked),
                LockedUsers = userRows.Count(user => user.IsLocked),
                AdminUsers = userRows.Count(user => user.IsAdmin),
                TotalSchedules = await _context.ScheduleItems.CountAsync(),
                TodaySchedules = await _context.ScheduleItems.CountAsync(item => item.StartTime.Date == today),
                ActiveOrUpcomingSchedules = await _context.ScheduleItems.CountAsync(item => item.EndTime >= now),
                SearchString = searchString ?? string.Empty,
                StatusFilter = statusFilter,
                Users = filteredUsers.ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                TempData["AdminMessage"] = $"Đã gán quyền Admin cho {user.Email}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Email == IdentitySeedData.AdminEmail)
            {
                TempData["AdminError"] = "Không thể hạ quyền tài khoản admin mặc định.";
                return RedirectToAction(nameof(Index));
            }

            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Count <= 1)
            {
                TempData["AdminError"] = "Hệ thống cần ít nhất một Admin.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
                if (!await _userManager.IsInRoleAsync(user, "User"))
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }
                TempData["AdminMessage"] = $"Đã hạ {user.Email} về quyền User.";
            }

            return RedirectToAction(nameof(Index));
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
                TempData["AdminMessage"] = $"Đã khóa tài khoản {user.Email}.";
            }
            else
            {
                TempData["AdminError"] = "Không thể khóa tài khoản admin mặc định.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);
                TempData["AdminMessage"] = $"Đã mở khóa tài khoản {user.Email}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (user.Email == IdentitySeedData.AdminEmail || user.Id == _userManager.GetUserId(User))
            {
                TempData["AdminError"] = "Không thể xóa tài khoản admin mặc định hoặc chính tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            var schedules = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);
            _context.ScheduleItems.RemoveRange(schedules);
            await _context.SaveChangesAsync();

            await _userManager.DeleteAsync(user);
            TempData["AdminMessage"] = $"Đã xóa tài khoản {user.Email}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
