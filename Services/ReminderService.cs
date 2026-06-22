using Microsoft.EntityFrameworkCore;
using schedule.Data;
using schedule.Models;

namespace schedule.Services
{
    public class ReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(IServiceProvider serviceProvider, ILogger<ReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    var now = DateTime.Now;

                    // ── 1. Nhắc lịch ScheduleItem ─────────────────────────────────────
                    var reminderItems = await context.ScheduleItems
                        .Where(item => item.ReceiverEmail != null
                            && item.ReceiverEmail != ""
                            && item.ReminderSentAt == null
                            && item.EndTime >= now
                            && item.StartTime <= now.AddMinutes(item.ReminderMinutes))
                        .ToListAsync(stoppingToken);

                    foreach (var item in reminderItems)
                    {
                        try
                        {
                            var isOngoing = item.StartTime <= now && item.EndTime >= now;
                            var subject = $"Nhắc lịch: {item.Title}";
                            var body = $"""
                                <p>Bạn có lịch <strong>{item.Title}</strong>.</p>
                                <p>Trạng thái: <strong>{(isOngoing ? "Đang diễn ra" : "Sắp diễn ra")}</strong></p>
                                <p>Thời gian: <strong>{item.StartTime:dd/MM/yyyy HH:mm} - {item.EndTime:dd/MM/yyyy HH:mm}</strong></p>
                                <p>Địa điểm: {item.Location}</p>
                                """;

                            await emailService.SendEmailAsync(item.ReceiverEmail!, subject, body);
                            item.ReminderSentAt = DateTime.Now;
                            _logger.LogInformation("[Reminder] ✅ Sent email for schedule {ScheduleId} \"{Title}\" → {Email}.",
                                item.Id, item.Title, item.ReceiverEmail);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx,
                                "[Reminder] ❌ Failed to send email for schedule {ScheduleId} \"{Title}\" → {Email}.",
                                item.Id, item.Title, item.ReceiverEmail);

                            // Đánh dấu đã xử lý để tránh lặp lại mỗi phút khi gặp lỗi SMTP vĩnh viễn
                            item.ReminderSentAt = DateTime.Now;
                        }
                    }

                    // ── 2. Thông báo Task quá hạn (trong vòng 7 ngày gần đây) ─────────
                    // Gửi email cho các task đã quá hạn trong vòng 7 ngày qua mà chưa được thông báo.
                    // Việc này giúp user nhận được thông báo kể cả khi app bị tắt vào thời điểm deadline trôi qua.
                    var sevenDaysAgo = now.AddDays(-7);
 
                    var overdueTasks = await context.TaskItems
                        .Where(t => t.CreatedByEmail != null
                            && t.CreatedByEmail != ""
                            && t.Status != TaskItemStatus.Completed
                            && t.Deadline >= sevenDaysAgo   // deadline trong vòng 7 ngày gần đây
                            && t.Deadline <= now            // đã qua giờ deadline
                            && t.OverdueEmailSentAt == null) // chưa gửi email
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation("[Overdue] Found {Count} overdue task(s) to notify at {Time}.",
                        overdueTasks.Count, now.ToString("HH:mm:ss"));

                    foreach (var task in overdueTasks)
                    {
                        try
                        {
                            var priorityLabel = task.Priority switch
                            {
                                TaskPriorityLevel.High   => "🔴 Khẩn cấp",
                                TaskPriorityLevel.Medium => "🟡 Vừa",
                                TaskPriorityLevel.Low    => "🟢 Thấp",
                                _                        => task.Priority.ToString()
                            };

                            var overdueMinutes = (int)(now - task.Deadline).TotalMinutes;
                            var overdueText = overdueMinutes < 60
                                ? $"{overdueMinutes} phút"
                                : $"{(int)(overdueMinutes / 60)} giờ {overdueMinutes % 60} phút";

                            var subject = $"⚠️ Task quá hạn: {task.Title}";
                            var body = $"""
                                <!DOCTYPE html>
                                <html lang="vi">
                                <head><meta charset="UTF-8"></head>
                                <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">
                                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:32px 0;">
                                    <tr><td align="center">
                                      <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">
                                        <!-- Header -->
                                        <tr>
                                          <td style="background:linear-gradient(135deg,#dc3545 0%,#a71d2a 100%);padding:28px 32px;text-align:center;">
                                            <div style="font-size:36px;margin-bottom:8px;">⚠️</div>
                                            <h1 style="margin:0;color:#ffffff;font-size:22px;font-weight:700;letter-spacing:0.3px;">Task Quá Hạn</h1>
                                            <p style="margin:6px 0 0;color:rgba(255,255,255,0.85);font-size:14px;">Trễ {overdueText}</p>
                                          </td>
                                        </tr>
                                        <!-- Body -->
                                        <tr>
                                          <td style="padding:28px 32px;">
                                            <p style="margin:0 0 20px;color:#444;font-size:15px;">Xin chào,</p>
                                            <p style="margin:0 0 20px;color:#444;font-size:15px;">Task sau đây của bạn đã <strong style="color:#dc3545;">quá hạn</strong> và chưa được hoàn thành:</p>

                                            <!-- Task card -->
                                            <table width="100%" cellpadding="0" cellspacing="0" style="background:#fff5f5;border:1px solid #f5c6cb;border-radius:8px;margin-bottom:24px;">
                                              <tr>
                                                <td style="padding:20px 24px;">
                                                  <div style="font-size:18px;font-weight:700;color:#1a1a2e;margin-bottom:14px;">{System.Net.WebUtility.HtmlEncode(task.Title)}</div>
                                                  <table cellpadding="0" cellspacing="0" width="100%">
                                                    <tr>
                                                      <td style="padding:5px 0;color:#666;font-size:13px;width:130px;">📅 Deadline</td>
                                                      <td style="padding:5px 0;color:#dc3545;font-weight:600;font-size:13px;">{task.Deadline:dd/MM/yyyy HH:mm}</td>
                                                    </tr>
                                                    <tr>
                                                      <td style="padding:5px 0;color:#666;font-size:13px;">🎯 Độ ưu tiên</td>
                                                      <td style="padding:5px 0;color:#333;font-size:13px;">{priorityLabel}</td>
                                                    </tr>
                                                    {(string.IsNullOrWhiteSpace(task.Description) ? "" : $"""
                                                    <tr>
                                                      <td style="padding:5px 0;color:#666;font-size:13px;vertical-align:top;">📝 Mô tả</td>
                                                      <td style="padding:5px 0;color:#333;font-size:13px;">{task.Description}</td>
                                                    </tr>
                                                    """)}
                                                  </table>
                                                </td>
                                              </tr>
                                            </table>

                                            <p style="margin:0 0 24px;color:#444;font-size:14px;">Hãy cập nhật trạng thái hoặc hoàn thành task để tránh ảnh hưởng đến tiến độ công việc.</p>

                                            <!-- CTA button -->
                                            <table cellpadding="0" cellspacing="0" width="100%">
                                              <tr>
                                                <td align="center">
                                                  <a href="#" style="display:inline-block;background:linear-gradient(135deg,#0d6efd,#0a58ca);color:#fff;text-decoration:none;padding:12px 32px;border-radius:8px;font-size:15px;font-weight:600;letter-spacing:0.3px;">Xem Task Ngay</a>
                                                </td>
                                              </tr>
                                            </table>
                                          </td>
                                        </tr>
                                        <!-- Footer -->
                                        <tr>
                                          <td style="background:#f8f9fa;padding:16px 32px;text-align:center;border-top:1px solid #e9ecef;">
                                            <p style="margin:0;color:#aaa;font-size:12px;">Email này được gửi tự động từ hệ thống Schedule Manager.<br>Vui lòng không trả lời email này.</p>
                                          </td>
                                        </tr>
                                      </table>
                                    </td></tr>
                                  </table>
                                </body>
                                </html>
                                """;

                            await emailService.SendEmailAsync(task.CreatedByEmail!, subject, body);
                            task.OverdueEmailSentAt = DateTime.Now;
                            _logger.LogInformation("[Overdue] ✅ Sent email for task {TaskId} \"{Title}\" → {Email}.",
                                task.Id, task.Title, task.CreatedByEmail);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx,
                                "[Overdue] ❌ Failed to send email for task {TaskId} \"{Title}\" → {Email}.",
                                task.Id, task.Title, task.CreatedByEmail);

                            // Đánh dấu đã xử lý để tránh lặp lại mỗi phút khi gặp lỗi SMTP vĩnh viễn
                            task.OverdueEmailSentAt = DateTime.Now;
                        }
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ReminderService] Unhandled error during reminder cycle.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
