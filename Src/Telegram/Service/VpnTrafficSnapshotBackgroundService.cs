using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XUi.Services.Interface;

namespace Telegram.Service;

/// <summary>
/// Периодически снимает снимок счётчиков трафика всех клиентов 3x-ui —
/// нужно для расчёта статистики за день/неделю/месяц/всё время, т.к.
/// счётчики 3x-ui могут обнуляться (сброс трафика, пересоздание клиента).
/// </summary>
public sealed class VpnTrafficSnapshotBackgroundService : BackgroundService
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VpnTrafficSnapshotBackgroundService> _logger;

    public VpnTrafficSnapshotBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<VpnTrafficSnapshotBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SnapshotInterval);

        do
        {
            await TryCaptureSnapshotAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TryCaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var xuiApiService = scope.ServiceProvider.GetRequiredService<IXUiApiService>();
            var snapshotRepo = scope.ServiceProvider.GetRequiredService<IVpnTrafficSnapshotRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var clients = await xuiApiService.GetClients(cancellationToken);
            if (clients is null or { Length: 0 })
            {
                return;
            }

            var now = DateTime.UtcNow;

            var snapshots = clients
                .Select(c => new VpnTrafficSnapshot
                {
                    ClientId = c.Id,
                    CapturedAtUtc = now,
                    TransferRx = c.TransferRx,
                    TransferTx = c.TransferTx,
                })
                .ToArray();

            snapshotRepo.AddRange(snapshots);
            await uow.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при снятии снимка трафика 3x-ui");
        }
    }
}
