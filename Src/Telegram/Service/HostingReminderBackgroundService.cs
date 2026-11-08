using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Telegram.Service;

/// <summary>
/// Периодически напоминает пользователям скинуться на хостинг.
/// Настройки берутся из БД при каждой проверке, поэтому изменения из админ-панели действуют без перезапуска
/// </summary>
public sealed class HostingReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly HostingReminderService _reminders;
    private readonly ILogger<HostingReminderBackgroundService> _logger;

    public HostingReminderBackgroundService(
        HostingReminderService reminders,
        ILogger<HostingReminderBackgroundService> logger)
    {
        _reminders = reminders;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Проверяем сразу при старте (вдруг пропустили срок, пока бот был выключен),
        // затем раз в час — не обязательно слать секунда-в-секунду.
        // Выключенная рассылка проверяется внутри: её могут включить из админ-панели в любой момент
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                await _reminders.SendIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // нормальная остановка
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при рассылке напоминаний об оплате хостинга");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
