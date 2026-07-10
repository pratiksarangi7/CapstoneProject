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
            _logger.LogInformation("DocumentExpiryWorker started. Cleanup runs every 24 hours.");

            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<DocumentCleanupService>();
                    var yesterday = DateTime.UtcNow.AddDays(-1).Date;
                    await service.RunCleanupAsync(asOfDate: yesterday, stoppingToken);
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
