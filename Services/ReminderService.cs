using Microsoft.EntityFrameworkCore;
using schedule.Data;

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

                    var reminderItems = await context.ScheduleItems
                        .Where(item => item.ReceiverEmail != null
                            && item.ReceiverEmail != ""
                            && item.ReminderSentAt == null
                            && item.EndTime >= now
                            && item.StartTime <= now.AddMinutes(item.ReminderMinutes))
                        .ToListAsync(stoppingToken);

                    foreach (var item in reminderItems)
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
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot send schedule reminders.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
