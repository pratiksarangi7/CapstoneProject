using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models.Enums;
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

        public async Task RunCleanupAsync(CancellationToken stoppingToken = default)
        {
            _logger.LogInformation("Starting document cleanup/expiry process.");
            
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            var today = DateTime.UtcNow.Date;

            var expiredDocs = await context.Documents
                .Include(d => d.Versions)
                .Where(d => d.ExpiryDate != null && d.ExpiryDate.Value.Date <= today 
                            && d.DocumentStatus != DocumentStatus.Expired)
                .ToListAsync(stoppingToken);

            foreach (var doc in expiredDocs)
            {
                doc.DocumentStatus = DocumentStatus.Expired;
                var currentVersion = doc.Versions.FirstOrDefault(v => v.IsCurrentVersion);
                if (currentVersion != null)
                {
                    string filePath = Path.Combine(env.ContentRootPath, "Uploads", currentVersion.StoredFileName);
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
            }
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Cleanup finished. {Count} documents expired.", expiredDocs.Count);
        }
    }
}