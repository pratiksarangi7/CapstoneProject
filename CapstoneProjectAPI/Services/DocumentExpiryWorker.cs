using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CapstoneProjectAPI.Services
{
    public class DocumentExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public DocumentExpiryWorker(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // PeriodicTimer is the modern, drift-resistant way to handle background intervals
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<DocumentCleanupService>();
                // Pass yesterday explicitly — the worker runs just after midnight so
                // documents whose ExpiryDate was yesterday are fully expired
                var yesterday = DateTime.UtcNow.AddDays(-1).Date;
                await service.RunCleanupAsync(asOfDate: yesterday, stoppingToken);
            }
        }
    }
}
