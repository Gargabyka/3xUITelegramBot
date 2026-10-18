using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Common.Helper;
using Telegram.Models;

namespace Telegram.Service;

/// <summary>
/// Периодически напоминает пользователям скинуться на хостинг
/// </summary>
public sealed class HostingReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _bot;
    private readonly HostingReminderOptions _options;
    private readonly ILogger<HostingReminderBackgroundService> _logger;

    public HostingReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient bot,
        IOptions<HostingReminderOptions> options,
        ILogger<HostingReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        // Проверяем сразу при старте (вдруг пропустили срок, пока бот был выключен),
        // затем раз в час — не обязательно слать секунда-в-секунду.
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            await TrySendReminderAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TrySendReminderAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var stateRepo = scope.ServiceProvider.GetRequiredService<IHostingReminderStateRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var state = await stateRepo.GetOrCreateAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Дата следующего цикла всегда считается от исходной LastPaymentDate (не цепочкой
            // AddMonths от предыдущей даты) — иначе для дат конца месяца дата необратимо "плывёт"
            // на более раннее число после пересечения более короткого месяца (см. HostingReminderScheduler).
            var nextCycle = state.RemindersSentCount + 1;
            if (HostingReminderScheduler.GetDueDate(_options.LastPaymentDate, _options.PeriodMonths, nextCycle) > today)
            {
                return;
            }

            // Если бот был выключен несколько циклов подряд — не заваливаем напоминаниями за каждый пропущенный
            // цикл, а один раз шлём актуальное и перескакиваем состояние на последний наступивший срок.
            while (HostingReminderScheduler.GetDueDate(_options.LastPaymentDate, _options.PeriodMonths, nextCycle + 1) <= today)
            {
                nextCycle++;
            }

            var users = await userRepo.GetUsersForReminderAsync(cancellationToken);

            foreach (var user in users)
            {
                try
                {
                    await _bot.SendMessage(user.TelegramId, _options.Message, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось отправить напоминание об оплате хостинга пользователю {TelegramId}", user.TelegramId);
                }
            }

            state.RemindersSentCount = nextCycle;
            state.LastSentAtUtc = DateTime.UtcNow;
            await uow.SaveChangesAsync(cancellationToken);
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
}
