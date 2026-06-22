using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
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

        public async Task<IActionResult> OnGetStartAsync(string provider, string? returnUrl = null)
        {
            return await StartExternalLoginAsync(provider, returnUrl);
        }

        public async Task<IActionResult> OnPostAsync(string provider, string? returnUrl = null)
        {
            return await StartExternalLoginAsync(provider, returnUrl);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                ErrorMessage = $"Google tr\u1ea3 l\u1ed7i: {remoteError}";
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Kh\u00f4ng \u0111\u1ecdc \u0111\u01b0\u1ee3c th\u00f4ng tin \u0111\u0103ng nh\u1eadp t\u1eeb Google. Vui l\u00f2ng th\u1eed l\u1ea1i.";
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
                ErrorMessage = "T\u00e0i kho\u1ea3n \u0111ang b\u1ecb kh\u00f3a t\u1ea1m th\u1eddi. Vui l\u00f2ng th\u1eed l\u1ea1i sau.";
                return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "Google kh\u00f4ng tr\u1ea3 v\u1ec1 email cho t\u00e0i kho\u1ea3n n\u00e0y.";
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

        private async Task<IActionResult> StartExternalLoginAsync(string provider, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                ErrorMessage = "Ch\u01b0a c\u1ea5u h\u00ecnh provider \u0111\u0103ng nh\u1eadp ngo\u00e0i.";
                return RedirectToPage("./Login", new { returnUrl });
            }

            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
            if (!schemes.Any(scheme => scheme.Name == provider))
            {
                ErrorMessage = "Google Login ch\u01b0a \u0111\u01b0\u1ee3c c\u1ea5u h\u00ecnh. H\u00e3y th\u00eam Google Client ID/Secret trong User Secrets r\u1ed3i ch\u1ea1y l\u1ea1i app.";
                return RedirectToPage("./Login", new { returnUrl });
            }

            if (provider.Equals("Google", StringComparison.OrdinalIgnoreCase) && IsPrivateIpHost(Request.Host.Host))
            {
                var localHost = Request.Host.Port.HasValue
                    ? $"localhost:{Request.Host.Port.Value}"
                    : "localhost";
                var localLoginUrl = Url.Page(
                    "./ExternalLogin",
                    pageHandler: "Start",
                    values: new { provider, returnUrl },
                    protocol: Request.Scheme,
                    host: localHost);

                if (!string.IsNullOrWhiteSpace(localLoginUrl))
                {
                    return Redirect(localLoginUrl);
                }
            }

            var redirectUrl = Url.Page(
                "./ExternalLogin",
                pageHandler: "Callback",
                values: new { returnUrl });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
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
