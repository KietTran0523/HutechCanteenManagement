using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuanLyCanTeenHutech.Services;

public class SepayExpiredOrderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SepayExpiredOrderBackgroundService> _logger;

    public SepayExpiredOrderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SepayExpiredOrderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarkExpiredOrdersAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await MarkExpiredOrdersAsync(stoppingToken);
        }
    }

    private async Task MarkExpiredOrdersAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sepayPaymentService = scope.ServiceProvider.GetRequiredService<SepayPaymentService>();
            var count = await sepayPaymentService.MarkExpiredOrdersAsync();

            if (count > 0)
            {
                _logger.LogInformation("Marked {Count} SePay orders as Expired.", count);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // App is stopping.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot mark expired SePay orders.");
        }
    }
}
