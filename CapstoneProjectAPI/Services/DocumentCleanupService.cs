using CapstoneProjectAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Services
{
    public class DocumentCleanupService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentCleanupService> _logger;

        public DocumentCleanupService(IServiceProvider serviceProvider, ILogger<DocumentCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Called by the background worker every day after midnight.
        /// - Expires documents whose ExpiryDate == yesterday (exact date, not cumulative)
        /// - Sends 7-day warning emails for documents expiring on today + 7 days
        /// </summary>
        public async Task RunDailyAsync(CancellationToken stoppingToken = default)
        {
            var yesterday = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1).Date, DateTimeKind.Utc);
            var warningDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(7), DateTimeKind.Utc);
            await RunAsync(expiryDate: yesterday, warningDate: warningDate, stoppingToken);
        }

        /// <summary>
        /// Called manually via the admin endpoint.
        /// - Expires documents whose ExpiryDate == asOfDate exactly
        /// - Sends 7-day warning emails for documents expiring on asOfDate + 7 days
        /// </summary>
        public async Task RunManualAsync(DateTime asOfDate, CancellationToken stoppingToken = default)
        {
            var expiryDate = DateTime.SpecifyKind(asOfDate.Date, DateTimeKind.Utc);
            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            if (expiryDate > today)
                throw new ArgumentException(
                    $"asOfDate cannot be in the future. Provided: {expiryDate:yyyy-MM-dd}, Today (UTC): {today:yyyy-MM-dd}.");

            var warningDate = DateTime.SpecifyKind(expiryDate.AddDays(7), DateTimeKind.Utc);
            await RunAsync(expiryDate: expiryDate, warningDate: warningDate, stoppingToken);
        }

        private async Task RunAsync(DateTime expiryDate, DateTime warningDate, CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Cleanup started | Expiring: {ExpiryDate:yyyy-MM-dd} | Warning for: {WarningDate:yyyy-MM-dd}.",
                expiryDate, warningDate);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var smtp = scope.ServiceProvider.GetRequiredService<SmtpService>();

            // ── 1. Send warning emails for documents expiring on warningDate ─────────
            var warningDocs = await context.Documents
                .Include(d => d.CreatedByUser)
                .Include(d => d.CurrentApprover)
                .Where(d => d.ExpiryDate != null
                         && d.ExpiryDate.Value.Date <= warningDate
                         && !d.IsExpired)
                .ToListAsync(stoppingToken);

            foreach (var doc in warningDocs)
            {
                // Notify the document owner
                try
                {
                    await smtp.SendEmailAsync(
                        doc.CreatedByUser.Email,
                        "Document Expiring Soon",
                        $"Your document '{doc.Title}' will expire on {warningDate:MMMM dd, yyyy}. " +
                        $"Please take the necessary action before it expires.");

                    _logger.LogInformation(
                        "Expiry warning sent to owner {Email} for document {DocumentId}.",
                        doc.CreatedByUser.Email, doc.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send expiry warning to owner {Email} for document {DocumentId}.",
                        doc.CreatedByUser.Email, doc.Id);
                }

                // Notify the current approver if one is assigned
                if (doc.CurrentApprover != null)
                {
                    try
                    {
                        await smtp.SendEmailAsync(
                            doc.CurrentApprover.Email,
                            "Approval Required: Document Expiring Soon",
                            $"The document '{doc.Title}' is pending your approval and will expire on {warningDate:MMMM dd, yyyy}. " +
                            $"Please review it before it expires.");

                        _logger.LogInformation(
                            "Expiry warning sent to approver {Email} for document {DocumentId}.",
                            doc.CurrentApprover.Email, doc.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to send expiry warning to approver {Email} for document {DocumentId}.",
                            doc.CurrentApprover.Email, doc.Id);
                    }
                }
            }

            _logger.LogInformation(
                "Warning phase done. {Count} document(s) expiring on {WarningDate:yyyy-MM-dd} notified.",
                warningDocs.Count, warningDate);

            // ── 2. Expire documents whose ExpiryDate == expiryDate exactly ───────────
            var expiredDocs = await context.Documents
                .Include(d => d.Versions)
                .Where(d => d.ExpiryDate != null
                         && d.ExpiryDate.Value.Date == expiryDate)
                .ToListAsync(stoppingToken);

            string uploadsFolder = Path.Combine(env.ContentRootPath, "Uploads");
            int filesDeleted = 0;
            int statusUpdated = 0;

            foreach (var doc in expiredDocs)
            {
                foreach (var version in doc.Versions)
                {
                    string filePath = Path.Combine(uploadsFolder, version.StoredFileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        filesDeleted++;
                        _logger.LogInformation(
                            "Deleted file {FileName} for document {DocumentId} version {Version}.",
                            version.StoredFileName, doc.Id, version.VersionNumber);
                    }
                }

                if (!doc.IsExpired)
                {
                    doc.IsExpired = true;
                    statusUpdated++;
                }
            }

            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "Cleanup done. Expiry date: {ExpiryDate:yyyy-MM-dd} | Documents: {DocCount} | Files deleted: {FileCount} | Marked expired: {StatusCount}.",
                expiryDate, expiredDocs.Count, filesDeleted, statusUpdated);
        }
    }
}
