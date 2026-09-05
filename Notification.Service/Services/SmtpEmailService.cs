using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Notification.Service.Services;

public class SmtpEmailOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = false;
    public string FromAddress { get; set; } = "noreply@stb.tn";
    public string FromName { get; set; } = "STB Gestion des Stagiaires";
    /// <summary>
    /// When true, emails are logged instead of sent — useful for local dev without Mailhog.
    /// </summary>
    public bool NoOp { get; set; } = false;
}

public class SmtpEmailService : IEmailService
{
    private readonly SmtpEmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (_options.NoOp)
        {
            _logger.LogInformation(
                "📧 [NoOp] Email to {To} — Subject: {Subject}\n{Body}",
                to, subject, body);
            return;
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_options.FromAddress, _options.FromName);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = false;

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Timeout = 10_000
            };

            if (!string.IsNullOrEmpty(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation(
                "📧 Email sent to {To} — Subject: {Subject}",
                to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Failed to send email to {To} — Subject: {Subject}",
                to, subject);
            // Don't throw — a failed email should not crash the consumer.
            // The event is already consumed; logging is sufficient for now.
        }
    }
}
