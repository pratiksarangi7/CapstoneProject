using CapstoneProjectAPI.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CapstoneProjectAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ─── Public interface methods ──────────────────────────────────────────

        public async Task SendDocumentUploadedToApproverAsync(
            string approverEmail,
            string approverName,
            string uploaderName,
            string documentTitle,
            string documentDescription,
            string targetDepartmentName,
            string fileName,
            long fileSize,
            string mimeType,
            int documentId,
            DateTime uploadedAt)
        {
            var subject = $"[DocFlow] New Document Awaiting Your Approval — \"{documentTitle}\"";

            var body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background:#f4f6f8; margin:0; padding:0; }}
    .wrapper {{ max-width:620px; margin:32px auto; background:#ffffff; border-radius:10px;
                box-shadow:0 2px 12px rgba(0,0,0,.08); overflow:hidden; }}
    .header {{ background:linear-gradient(135deg,#1a73e8,#0d47a1); padding:32px 36px; color:#fff; }}
    .header h1 {{ margin:0; font-size:22px; font-weight:600; }}
    .header p  {{ margin:6px 0 0; font-size:14px; opacity:.85; }}
    .body {{ padding:32px 36px; }}
    .greeting {{ font-size:16px; color:#202124; margin-bottom:20px; }}
    .card {{ background:#f8f9fa; border-left:4px solid #1a73e8; border-radius:6px;
              padding:20px 24px; margin-bottom:24px; }}
    .card table {{ width:100%; border-collapse:collapse; }}
    .card td {{ padding:6px 0; font-size:14px; color:#3c4043; vertical-align:top; }}
    .card td.label {{ font-weight:600; width:170px; color:#202124; }}
    .cta {{ display:inline-block; background:#1a73e8; color:#fff !important; text-decoration:none;
             padding:12px 28px; border-radius:6px; font-size:14px; font-weight:600; margin-top:8px; }}
    .footer {{ background:#f4f6f8; padding:20px 36px; font-size:12px; color:#80868b;
                border-top:1px solid #e0e0e0; text-align:center; }}
  </style>
</head>
<body>
<div class=""wrapper"">
  <div class=""header"">
    <h1>📄 Document Pending Your Approval</h1>
    <p>A new document has been submitted to your review queue.</p>
  </div>
  <div class=""body"">
    <p class=""greeting"">Hello <strong>{approverName}</strong>,</p>
    <p style=""font-size:15px;color:#3c4043;"">
      <strong>{uploaderName}</strong> has uploaded a document that is awaiting your approval.
      Please review the details below and take action at your earliest convenience.
    </p>

    <div class=""card"">
      <table>
        <tr><td class=""label"">Document ID</td><td>#{documentId}</td></tr>
        <tr><td class=""label"">Title</td><td>{documentTitle}</td></tr>
        <tr><td class=""label"">Description</td><td>{(string.IsNullOrWhiteSpace(documentDescription) ? "<em>No description provided</em>" : documentDescription)}</td></tr>
        <tr><td class=""label"">Uploaded By</td><td>{uploaderName}</td></tr>
        <tr><td class=""label"">Target Department</td><td>{targetDepartmentName}</td></tr>
        <tr><td class=""label"">File Name</td><td>{fileName}</td></tr>
        <tr><td class=""label"">File Size</td><td>{FormatFileSize(fileSize)}</td></tr>
        <tr><td class=""label"">File Type</td><td>{mimeType}</td></tr>
        <tr><td class=""label"">Uploaded At</td><td>{uploadedAt:dd MMM yyyy, HH:mm} UTC</td></tr>
      </table>
    </div>

    <p style=""font-size:14px;color:#3c4043;"">
      Please log in to the DocFlow portal to review and approve or reject this document.
    </p>
  </div>
  <div class=""footer"">
    This is an automated notification from DocFlow. Please do not reply to this email.
  </div>
</div>
</body>
</html>";

            await SendEmailAsync(approverEmail, approverName, subject, body);
        }

        public async Task SendDocumentApprovedToUploaderAsync(
            string uploaderEmail,
            string uploaderName,
            string approverName,
            string documentTitle,
            string documentDescription,
            string targetDepartmentName,
            int documentId,
            DateTimeOffset approvedAt)
        {
            var subject = $"[DocFlow] Your Document Has Been Approved — \"{documentTitle}\"";

            var body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background:#f4f6f8; margin:0; padding:0; }}
    .wrapper {{ max-width:620px; margin:32px auto; background:#ffffff; border-radius:10px;
                box-shadow:0 2px 12px rgba(0,0,0,.08); overflow:hidden; }}
    .header {{ background:linear-gradient(135deg,#34a853,#1e7e34); padding:32px 36px; color:#fff; }}
    .header h1 {{ margin:0; font-size:22px; font-weight:600; }}
    .header p  {{ margin:6px 0 0; font-size:14px; opacity:.85; }}
    .body {{ padding:32px 36px; }}
    .greeting {{ font-size:16px; color:#202124; margin-bottom:20px; }}
    .card {{ background:#f8f9fa; border-left:4px solid #34a853; border-radius:6px;
              padding:20px 24px; margin-bottom:24px; }}
    .card table {{ width:100%; border-collapse:collapse; }}
    .card td {{ padding:6px 0; font-size:14px; color:#3c4043; vertical-align:top; }}
    .card td.label {{ font-weight:600; width:170px; color:#202124; }}
    .badge {{ display:inline-block; background:#e6f4ea; color:#1e7e34; font-weight:700;
               padding:4px 12px; border-radius:20px; font-size:13px; margin-bottom:20px; }}
    .footer {{ background:#f4f6f8; padding:20px 36px; font-size:12px; color:#80868b;
                border-top:1px solid #e0e0e0; text-align:center; }}
  </style>
</head>
<body>
<div class=""wrapper"">
  <div class=""header"">
    <h1>✅ Document Approved!</h1>
    <p>Great news — your document has been fully approved.</p>
  </div>
  <div class=""body"">
    <p class=""greeting"">Hello <strong>{uploaderName}</strong>,</p>
    <span class=""badge"">✓ APPROVED</span>
    <p style=""font-size:15px;color:#3c4043;"">
      Your document <strong>""{documentTitle}""</strong> has been reviewed and
      <strong>fully approved</strong> by <strong>{approverName}</strong>.
      No further action is required on your part.
    </p>

    <div class=""card"">
      <table>
        <tr><td class=""label"">Document ID</td><td>#{documentId}</td></tr>
        <tr><td class=""label"">Title</td><td>{documentTitle}</td></tr>
        <tr><td class=""label"">Description</td><td>{(string.IsNullOrWhiteSpace(documentDescription) ? "<em>No description provided</em>" : documentDescription)}</td></tr>
        <tr><td class=""label"">Target Department</td><td>{targetDepartmentName}</td></tr>
        <tr><td class=""label"">Approved By</td><td>{approverName}</td></tr>
        <tr><td class=""label"">Approved At</td><td>{approvedAt:dd MMM yyyy, HH:mm} UTC</td></tr>
      </table>
    </div>

    <p style=""font-size:14px;color:#3c4043;"">
      You can view your approved document at any time in the DocFlow portal.
    </p>
  </div>
  <div class=""footer"">
    This is an automated notification from DocFlow. Please do not reply to this email.
  </div>
</div>
</body>
</html>";

            await SendEmailAsync(uploaderEmail, uploaderName, subject, body);
        }

        // ─── Private helpers ───────────────────────────────────────────────────

        private async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var smtpHost     = _config["Email:SmtpHost"]     ?? "smtp.gmail.com";
            var smtpPortStr  = _config["Email:SmtpPort"]     ?? "587";
            var senderEmail  = _config["Email:SenderEmail"]  ?? string.Empty;
            var senderName   = _config["Email:SenderName"]   ?? "DocFlow";
            var username     = _config["Email:Username"]      ?? string.Empty;
            var password     = _config["Email:Password"]      ?? string.Empty;

            if (!int.TryParse(smtpPortStr, out int smtpPort))
                smtpPort = 587;

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(quit: true);

                _logger.LogInformation(
                    "[EmailService] Email successfully sent to {ToEmail} | Subject: {Subject}",
                    toEmail, subject);
            }
            catch (Exception ex)
            {
                // Email failures must never propagate — this runs fire-and-forget.
                _logger.LogWarning(
                    ex,
                    "[EmailService] Failed to send email to {ToEmail} | Subject: {Subject}",
                    toEmail, subject);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024)     return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}
