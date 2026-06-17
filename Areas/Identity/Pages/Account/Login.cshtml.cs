using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace schedule.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public LoginModel(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public string ReturnUrl { get; set; } = "/";

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau.");
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return Page();
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Ghi nhớ đăng nhập")]
            public bool RememberMe { get; set; }
        }
    }
}
