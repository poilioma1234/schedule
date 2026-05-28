using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using schedule.ViewModels;

namespace schedule.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.OrderBy(user => user.Email).ToList();
            var model = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? user.UserName ?? "",
                    Roles = string.Join(", ", roles),
                    IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow
                });
            }

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
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
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
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && user.Email != Data.IdentitySeedData.AdminEmail)
            {
                await _userManager.DeleteAsync(user);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
