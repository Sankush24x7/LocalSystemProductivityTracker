using System.Net;
using System.Net.Mail;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class EmailService
{
    public void SendReport(AppSettings settings, string htmlPath, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(settings.MailFrom) || string.IsNullOrWhiteSpace(settings.MailTo) || string.IsNullOrWhiteSpace(settings.MailPassword))
        {
            throw new InvalidOperationException("Mail From/To/Password must be configured in Settings.");
        }

        if (!File.Exists(htmlPath) || !File.Exists(jsonPath))
        {
            throw new FileNotFoundException("Required report files were not found.");
        }

        using var message = new MailMessage(settings.MailFrom.Trim(), settings.MailTo.Trim())
        {
            Subject = $"Productivity Report - {DateTime.Now:yyyy-MM-dd}",
            IsBodyHtml = true,
            Body = File.ReadAllText(htmlPath)
        };

        message.Attachments.Add(new Attachment(htmlPath));
        message.Attachments.Add(new Attachment(jsonPath));

        using var smtp = new SmtpClient(settings.SmtpHost.Trim(), settings.SmtpPort)
        {
            EnableSsl = settings.SmtpUseSsl,
            Credentials = new NetworkCredential(settings.MailFrom.Trim(), settings.MailPassword)
        };

        smtp.Send(message);
    }
}
