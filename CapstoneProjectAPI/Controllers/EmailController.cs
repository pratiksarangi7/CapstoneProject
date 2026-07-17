using System.Security.Claims;
using CapstoneProjectAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CapstoneProjectAPI.Controllers
{
    /// <summary>
    /// Provides endpoints for manually testing email notifications.
    /// These are developer/admin test endpoints — emails in the normal flow are
    /// sent automatically by DocumentService (fire-and-forget).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("FixedPerUser")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(IEmailService emailService, ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Sends a test "document uploaded" notification to the caller's own email address.
        /// </summary>
        [HttpPost("test/upload-notification")]
        public IActionResult TestUploadNotification()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName    = User.FindFirstValue(ClaimTypes.Name) ?? "Test User";
            var userEmail   = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            if (string.IsNullOrEmpty(userEmail))
                return BadRequest(new { message = "No email found in token claims." });

            _logger.LogInformation(
                "[EmailController] Test upload-notification requested by user {UserId}.", userIdClaim);

            // Fire-and-forget
            _ = Task.Run(() => _emailService.SendDocumentUploadedToApproverAsync(
                approverEmail:        userEmail,
                approverName:         userName,
                uploaderName:         "John Doe (Test Uploader)",
                documentTitle:        "Sample Contract Agreement",
                documentDescription:  "This is a test document description for the email preview.",
                targetDepartmentName: "Finance Department",
                fileName:             "contract_v1.pdf",
                fileSize:             1_048_576,
                mimeType:             "application/pdf",
                documentId:           9999,
                uploadedAt:           DateTime.UtcNow));

            return Accepted(new { message = $"Test upload-notification email dispatched to {userEmail}." });
        }

        /// <summary>
        /// Sends a test "document approved" notification to the caller's own email address.
        /// </summary>
        [HttpPost("test/approval-notification")]
        public IActionResult TestApprovalNotification()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName    = User.FindFirstValue(ClaimTypes.Name) ?? "Test User";
            var userEmail   = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            if (string.IsNullOrEmpty(userEmail))
                return BadRequest(new { message = "No email found in token claims." });

            _logger.LogInformation(
                "[EmailController] Test approval-notification requested by user {UserId}.", userIdClaim);

            // Fire-and-forget
            _ = Task.Run(() => _emailService.SendDocumentApprovedToUploaderAsync(
                uploaderEmail:        userEmail,
                uploaderName:         userName,
                approverName:         "Jane Smith (Test Approver)",
                documentTitle:        "Sample Contract Agreement",
                documentDescription:  "This is a test document description for the email preview.",
                targetDepartmentName: "Finance Department",
                documentId:           9999,
                approvedAt:           DateTimeOffset.UtcNow));

            return Accepted(new { message = $"Test approval-notification email dispatched to {userEmail}." });
        }
    }
}
