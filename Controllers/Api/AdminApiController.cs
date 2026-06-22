using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.DTOs;
using schedule.Models;
using schedule.Services;

namespace schedule.Controllers.Api
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;

        public AdminApiController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        [HttpGet("users")]
        [ProducesResponseType(typeof(IEnumerable<AdminUserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers([FromQuery] string? searchString, [FromQuery] string? statusFilter)
        {
            var users = await _userManager.Users.ToListAsync();
            var userRows = new List<AdminUserDto>();
            var today = DateTime.Today;
            var now = DateTime.Now;

            foreach (var user in users)
            {
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                var avatarPath = profile?.AvatarPath ?? "/assets/img/default-avatar.png";

                var roles = await _userManager.GetRolesAsync(user);
                var scheduleQuery = _context.ScheduleItems.Where(item => item.CreatedByUserId == user.Id);
                var taskQuery = _context.TaskItems.Where(item => item.CreatedByUserId == user.Id);

                userRows.Add(new AdminUserDto
                {
                    Id = user.Id,
                    Email = user.Email ?? user.UserName ?? "",
                    AvatarPath = avatarPath,
                    Roles = roles.Any() ? string.Join(", ", roles) : "User",
                    IsAdmin = roles.Contains("Admin"),
                    IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                    ScheduleCount = await scheduleQuery.CountAsync(),
                    TodayScheduleCount = await scheduleQuery.CountAsync(item => item.StartTime.Date == today),
                    ActiveOrUpcomingScheduleCount = await scheduleQuery.CountAsync(item => item.EndTime >= now),
                    TotalTaskCount = await taskQuery.CountAsync(),
                    CompletedTaskCount = await taskQuery.CountAsync(item => item.Status == TaskItemStatus.Completed),
                    OverdueTaskCount = await taskQuery.CountAsync(item => item.Status != TaskItemStatus.Completed && item.Deadline < now),
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

            filteredUsers = statusFilter?.ToLowerInvariant() switch
            {
                "locked" => filteredUsers.Where(user => user.IsLocked),
                "admin" => filteredUsers.Where(user => user.IsAdmin),
                "user" => filteredUsers.Where(user => !user.IsAdmin),
                _ => filteredUsers
            };

            return Ok(filteredUsers);
        }

        [HttpPost("users/{id}/lock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LockUser(string id, [FromBody] ResolveReportRequestDto req)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            if (user.Email == IdentitySeedData.AdminEmail)
            {
                return BadRequest("Không thể khóa tài khoản admin mặc định.");
            }

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            // Send lock notification email
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    var reason = string.IsNullOrWhiteSpace(req.AdminNote) ? "Tài khoản bị khóa trực tiếp bởi quản trị viên." : req.AdminNote;
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "[HUTECH Schedule] Tài khoản bị khóa",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#dc2626;'>&#128274; Tài khoản đã bị khóa</h2>" +
                        $"<p>Tài khoản <strong>{user.Email}</strong> đã bị khóa bởi quản trị viên hệ thống HUTECH Schedule.</p>" +
                        "<div style='background:#fef2f2;border-left:4px solid #dc2626;padding:16px;border-radius:6px;margin:16px 0;'>" +
                        "<p style='margin:0;'><strong>Lý do khóa:</strong></p>" +
                        $"<p style='margin:8px 0 0;'>{WebUtility.HtmlEncode(reason)}</p>" +
                        "</div>" +
                        "<p>Tài khoản của bạn đã bị tạm khóa. Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.</p>" +
                        $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "</div>");
                }
                catch { /* ignore email error */ }
            }

            return Ok(new { success = true, message = $"Đã khóa tài khoản {user.Email} thành công." });
        }

        [HttpPost("users/{id}/unlock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnlockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);

            // Send unlock notification email
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "[HUTECH Schedule] Tài khoản đã được mở khóa",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#16a34a;'>&#128275; Tài khoản đã được mở khóa</h2>" +
                        $"<p>Tài khoản <strong>{user.Email}</strong> đã được mở khóa bởi quản trị viên.</p>" +
                        "<p>Bây giờ bạn có thể đăng nhập bình thường vào hệ thống để tiếp tục lập lịch và quản lý công việc.</p>" +
                        $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "</div>");
                }
                catch { /* ignore email error */ }
            }

            return Ok(new { success = true, message = $"Đã mở khóa tài khoản {user.Email} thành công." });
        }

        [HttpGet("reports")]
        [ProducesResponseType(typeof(IEnumerable<AdminReportDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReports([FromQuery] string? statusFilter)
        {
            var query = _context.UserReports.AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (Enum.TryParse<ReportStatus>(statusFilter, true, out var status))
                {
                    query = query.Where(r => r.Status == status);
                }
            }

            var reports = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            var dtos = new List<AdminReportDto>();

            foreach (var r in reports)
            {
                var reportedUser = await _userManager.FindByIdAsync(r.ReportedUserId);
                var reporterUser = r.ReporterUserId != null ? await _userManager.FindByIdAsync(r.ReporterUserId) : null;

                dtos.Add(new AdminReportDto
                {
                    Id = r.Id,
                    ReportedUserId = r.ReportedUserId,
                    ReportedUserEmail = reportedUser?.Email ?? "Không rõ email",
                    ReporterUserId = r.ReporterUserId,
                    ReporterUserEmail = reporterUser?.Email ?? "Ẩn danh",
                    Reason = r.Reason,
                    Category = r.Category,
                    CreatedAt = r.CreatedAt,
                    Status = r.Status.ToString(),
                    AdminNote = r.AdminNote,
                    HandledAt = r.HandledAt
                });
            }

            return Ok(dtos);
        }

        [HttpPost("reports/{id}/warn")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> WarnFromReport(int id, [FromBody] ResolveReportRequestDto req)
        {
            var report = await _context.UserReports.FindAsync(id);
            if (report == null)
            {
                return NotFound("Báo cáo không tồn tại.");
            }

            report.Status = ReportStatus.Warned;
            report.AdminNote = req.AdminNote;
            report.HandledAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Send warning email to reported user
            var reportedUser = await _userManager.FindByIdAsync(report.ReportedUserId);
            if (reportedUser?.Email != null)
            {
                try
                {
                    var note = string.IsNullOrWhiteSpace(req.AdminNote) ? "Tài khoản của bạn đã nhận được cảnh báo do vi phạm chính sách." : req.AdminNote;
                    await _emailService.SendEmailAsync(
                        reportedUser.Email,
                        "[HUTECH Schedule] Cảnh báo tài khoản",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#d97706;'>&#9888; Cảnh báo từ quản trị viên</h2>" +
                        $"<p>Tài khoản của bạn (<strong>{reportedUser.Email}</strong>) đã nhận được cảnh báo từ đội quản trị hệ thống HUTECH Schedule.</p>" +
                        "<div style='background:#fffbeb;border-left:4px solid #d97706;padding:16px;border-radius:6px;margin:16px 0;'>" +
                        "<p style='margin:0;'><strong>Chi tiết cảnh báo / Lý do:</strong></p>" +
                        $"<p style='margin:8px 0 0;'>{WebUtility.HtmlEncode(note)}</p>" +
                        "</div>" +
                        "<p>Vui lòng tuân thủ điều khoản dịch vụ để tránh việc tài khoản bị khóa vĩnh viễn.</p>" +
                        $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "</div>");
                }
                catch { /* ignore email error */ }
            }

            return Ok(new { success = true, message = "Đã gửi cảnh báo và cập nhật báo cáo." });
        }

        [HttpPost("reports/{id}/lock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LockFromReport(int id, [FromBody] ResolveReportRequestDto req)
        {
            var report = await _context.UserReports.FindAsync(id);
            if (report == null)
            {
                return NotFound("Báo cáo không tồn tại.");
            }

            var reportedUser = await _userManager.FindByIdAsync(report.ReportedUserId);
            if (reportedUser == null)
            {
                return NotFound("Người dùng bị báo cáo không tồn tại.");
            }

            if (reportedUser.Email == IdentitySeedData.AdminEmail)
            {
                return BadRequest("Không thể khóa tài khoản admin mặc định.");
            }

            report.Status = ReportStatus.Locked;
            report.AdminNote = req.AdminNote;
            report.HandledAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _userManager.SetLockoutEnabledAsync(reportedUser, true);
            await _userManager.SetLockoutEndDateAsync(reportedUser, DateTimeOffset.UtcNow.AddYears(100));

            // Send lock notification email
            if (!string.IsNullOrWhiteSpace(reportedUser.Email))
            {
                try
                {
                    var reason = string.IsNullOrWhiteSpace(req.AdminNote) ? report.Reason : req.AdminNote;
                    await _emailService.SendEmailAsync(
                        reportedUser.Email,
                        "[HUTECH Schedule] Tài khoản bị khóa",
                        "<div style='font-family:sans-serif;max-width:600px;margin:auto;padding:24px;border:1px solid #e5e7eb;border-radius:12px;'>" +
                        "<h2 style='color:#dc2626;'>&#128274; Tài khoản đã bị khóa</h2>" +
                        $"<p>Tài khoản <strong>{reportedUser.Email}</strong> đã bị khóa bởi quản trị viên hệ thống HUTECH Schedule.</p>" +
                        "<div style='background:#fef2f2;border-left:4px solid #dc2626;padding:16px;border-radius:6px;margin:16px 0;'>" +
                        "<p style='margin:0;'><strong>Lý do khóa:</strong></p>" +
                        $"<p style='margin:8px 0 0;'>{WebUtility.HtmlEncode(reason)}</p>" +
                        "</div>" +
                        "<p>Tài khoản của bạn đã bị tạm khóa. Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.</p>" +
                        $"<p style='color:#6b7280;font-size:0.85rem;'>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                        "</div>");
                }
                catch { /* ignore email error */ }
            }

            return Ok(new { success = true, message = "Đã khóa tài khoản bị báo cáo vĩnh viễn và xử lý báo cáo." });
        }

        [HttpPost("reports/{id}/dismiss")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DismissReport(int id, [FromBody] ResolveReportRequestDto req)
        {
            var report = await _context.UserReports.FindAsync(id);
            if (report == null)
            {
                return NotFound("Báo cáo không tồn tại.");
            }

            report.Status = ReportStatus.Dismissed;
            report.AdminNote = req.AdminNote;
            report.HandledAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã bỏ qua báo cáo." });
        }
    }
}
