using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace QuanLyCanTeenHutech.Services;

public class BrevoEmailSender : IEmailSender
{
    private readonly SmtpSettings _smtpSettings;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(IOptions<SmtpSettings> smtpSettings, ILogger<BrevoEmailSender> logger)
    {
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_smtpSettings.Host) ||
            string.IsNullOrWhiteSpace(_smtpSettings.Username) ||
            string.IsNullOrWhiteSpace(_smtpSettings.Password) ||
            string.IsNullOrWhiteSpace(_smtpSettings.FromEmail))
        {
            throw new InvalidOperationException("SMTP chưa được cấu hình đủ trong appsettings/User Secrets/Environment Variables.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
        {
            Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Đã gửi email '{Subject}' tới {Email}", subject, email);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Gửi email qua Brevo SMTP thất bại tới {Email}", email);
            throw;
        }
    }
}
