using Microsoft.Extensions.Options;
using schedule.Models;
using System.Net;
using System.Net.Mail;

namespace schedule.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (!_settings.EnableEmail)
            {
                _logger.LogInformation("Email is disabled. Reminder for {Email}: {Subject}", toEmail, subject);
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword)
            };

            await client.SendMailAsync(message);
        }
    }
}
