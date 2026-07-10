using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CapstoneProjectAPI.Services
{
    public class DocumentExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentExpiryWorker> _logger;

        public DocumentExpiryWorker(IServiceProvider serviceProvider, ILogger<DocumentExpiryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DocumentExpiryWorker started. Runs at midnight UTC daily.");

            try
            {
                // Run immediately on startup to catch any days missed while the app was down
                _logger.LogInformation("DocumentExpiryWorker: running startup cleanup.");
                using (var scope = _serviceProvider.CreateScope())
                {
                    var service = scope.ServiceProvider.GetRequiredService<DocumentCleanupService>();
                    await service.RunDailyAsync(stoppingToken);
                }

                // Then wait until next midnight UTC and repeat every 24 hours
                while (!stoppingToken.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var nextMidnight = now.Date.AddDays(1); // tomorrow at 00:00:00 UTC
                    var delay = nextMidnight - now;

                    _logger.LogInformation(
                        "Next cleanup scheduled at {NextRun:yyyy-MM-dd HH:mm:ss} UTC (in {Hours}h {Minutes}m).",
                        nextMidnight, (int)delay.TotalHours, delay.Minutes);

                    await Task.Delay(delay, stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<DocumentCleanupService>();
                    await service.RunDailyAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — host is stopping, not an error
                _logger.LogInformation("DocumentExpiryWorker is stopping.");
            }
        }
    }
}
