using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Telegram.Service;

/// <summary>
/// Периодически снимает снимок счётчиков трафика всех клиентов 3x-ui —
/// нужно для расчёта статистики за день/неделю/месяц/всё время, т.к.
/// счётчики 3x-ui могут обнуляться (сброс трафика, пересоздание клиента).
/// </summary>
public sealed class VpnTrafficSnapshotBackgroundService : BackgroundService
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CompactionInterval = TimeSpan.FromDays(1);

    // Периоды статистики — до 30 дней; более старые снимки нужны только для итога «за всё время»
    private static readonly TimeSpan DetailedRetention = TimeSpan.FromDays(35);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VpnTrafficSnapshotBackgroundService> _logger;
    private DateTime _lastCompactionUtc = DateTime.MinValue;

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
            await TrySyncPeersAsync(stoppingToken);

            if (DateTime.UtcNow - _lastCompactionUtc >= CompactionInterval)
            {
                await TryCompactSnapshotsAsync(stoppingToken);
                _lastCompactionUtc = DateTime.UtcNow;
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Подтягивает изменения, сделанные в панелях мимо бота (новые, переименованные, удалённые клиенты)
    /// </summary>
    private async Task TrySyncPeersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<PeerSyncService>();

            var result = await syncService.SyncAsync(cancellationToken);
            if (result.Added + result.Updated + result.Removed > 0)
            {
                _logger.LogInformation(
                    "Синхронизация peer'ов: добавлено {Added}, обновлено {Updated}, удалено {Removed}",
                    result.Added,
                    result.Updated,
                    result.Removed);
            }
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка синхронизации peer'ов с VPN-серверами");
        }
    }

    private async Task TryCompactSnapshotsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var snapshotRepo = scope.ServiceProvider.GetRequiredService<IVpnTrafficSnapshotRepository>();

            var removed = await snapshotRepo.CompactAsync(DateTime.UtcNow - DetailedRetention, cancellationToken);
            if (removed > 0)
            {
                _logger.LogInformation("Сжатие снимков трафика: удалено {Removed}", removed);
            }
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сжатии снимков трафика");
        }
    }

    private async Task TryCaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var xuiApiService = scope.ServiceProvider.GetRequiredService<IVpnApiService>();
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
