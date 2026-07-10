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
        /// Cleans up documents whose ExpiryDate is on or before <paramref name="asOfDate"/>.
        /// Deletes all physical version files and marks the document as Expired.
        /// When called by the background worker (after midnight) asOfDate defaults to yesterday,
        /// so only documents that fully expired the previous day are processed.
        /// </summary>
        public async Task RunCleanupAsync(DateTime? asOfDate = null, CancellationToken stoppingToken = default)
        {
            // Default to yesterday — safe cutoff when the worker runs just after midnight.
            // Force UTC kind — query string parsing produces Kind=Unspecified which Npgsql rejects.
            var rawDate = (asOfDate ?? DateTime.UtcNow.AddDays(-1)).Date;
            var cutoff = DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);

            _logger.LogInformation("Starting document cleanup for expiry date <= {Cutoff:yyyy-MM-dd}.", cutoff);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            string uploadsFolder = Path.Combine(env.ContentRootPath, "Uploads");

            // Fetch all docs expired on or before the cutoff, regardless of current status
            // so re-running cleanup is always idempotent and catches leftover files
            var expiredDocs = await context.Documents
                .Include(d => d.Versions)
                .Where(d => d.ExpiryDate != null && d.ExpiryDate.Value.Date <= cutoff)
                .ToListAsync(stoppingToken);

            int filesDeleted = 0;
            int statusUpdated = 0;

            foreach (var doc in expiredDocs)
            {
                // Delete physical files for every version, not just the current one
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
                "Cleanup finished. Cutoff: {Cutoff:yyyy-MM-dd} | Documents processed: {DocCount} | Files deleted: {FileCount} | Status updated: {StatusCount}.",
                cutoff, expiredDocs.Count, filesDeleted, statusUpdated);
        }
    }
}
