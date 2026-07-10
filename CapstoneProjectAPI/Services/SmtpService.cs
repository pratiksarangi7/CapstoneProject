using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CapstoneProjectAPI.Services;

public class SmtpService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpService> _logger;

    public SmtpService(IConfiguration config, ILogger<SmtpService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var host = _config["SmtpSettings:Host"] ?? throw new InvalidOperationException("SmtpSettings:Host is not configured.");
        var port = _config.GetValue<int>("SmtpSettings:Port", 587);
        var senderEmail = _config["SmtpSettings:SenderEmail"] ?? throw new InvalidOperationException("SmtpSettings:SenderEmail is not configured.");
        var password = _config["SmtpSettings:Password"] ?? throw new InvalidOperationException("SmtpSettings:Password is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Compliance System", senderEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        // StartTls is required for Gmail on port 587
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(senderEmail, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email sent to {ToEmail} | Subject: {Subject}.", toEmail, subject);
    }
}
