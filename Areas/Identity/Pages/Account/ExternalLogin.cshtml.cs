using System.Security.Claims;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace schedule.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public ExternalLoginModel(
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager)
        {
            _roleManager = roleManager;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public string ReturnUrl { get; set; } = "/";

        [TempData]
        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            return RedirectToPage("./Login");
        }

        public async Task<IActionResult> OnPostAsync(string provider, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                ErrorMessage = "Chưa cấu hình provider đăng nhập ngoài.";
                return RedirectToPage("./Login", new { returnUrl });
            }

            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
            if (!schemes.Any(scheme => scheme.Name == provider))
            {
                ErrorMessage = "Google Login chưa được cấu hình. Hãy thêm Google Client ID/Secret trong User Secrets rồi chạy lại app.";
                return RedirectToPage("./Login", new { returnUrl });
            }

            if (provider.Equals("Google", StringComparison.OrdinalIgnoreCase) && IsPrivateIpHost(Request.Host.Host))
            {
                ErrorMessage = "Google khong ho tro dang nhap OAuth bang dia chi IP noi bo. Hay mo bang http://localhost:5299 tren may dang chay app, hoac dung ten mien HTTPS public nhu ngrok/Cloudflare Tunnel va them /signin-google vao Google Cloud.";
                return RedirectToPage("./Login", new { returnUrl });
            }

            var redirectUrl = Url.Page(
                "./ExternalLogin",
                pageHandler: "Callback",
                values: new { returnUrl });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                ErrorMessage = $"Google trả lỗi: {remoteError}";
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Không đọc được thông tin đăng nhập từ Google. Vui lòng thử lại.";
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                return LocalRedirect(ReturnUrl);
            }

            if (signInResult.IsLockedOut)
            {
                ErrorMessage = "Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau.";
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "Google không trả về email cho tài khoản này.";
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    ErrorMessage = string.Join(" ", createResult.Errors.Select(error => error.Description));
                    return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
                }
            }
            else if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded && !addLoginResult.Errors.Any(error => error.Code == "LoginAlreadyAssociated"))
            {
                ErrorMessage = string.Join(" ", addLoginResult.Errors.Select(error => error.Description));
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            await EnsureUserRoleAsync(user);
            await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);

            return LocalRedirect(ReturnUrl);
        }

        private async Task EnsureUserRoleAsync(IdentityUser user)
        {
            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
            }

            if (!await _userManager.IsInRoleAsync(user, "User"))
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
        }

        private static bool IsPrivateIpHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            host = host.Trim('[', ']');

            if (!IPAddress.TryParse(host, out var ipAddress) || IPAddress.IsLoopback(ipAddress))
            {
                return false;
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254);
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var bytes = ipAddress.GetAddressBytes();
                return ipAddress.IsIPv6LinkLocal
                    || ipAddress.IsIPv6SiteLocal
                    || (bytes[0] & 0xfe) == 0xfc;
            }

            return false;
        }
    }
}
