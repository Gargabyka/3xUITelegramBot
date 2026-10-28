using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Service.Interface;

namespace Telegram.Service;

/// <summary>
/// Фоновый обработчик сообщений Telegram
/// </summary>
public class TelegramLongPollingService : BackgroundService
{
    // Сколько обновлений обрабатывается одновременно (разных чатов)
    private const int MaxConcurrency = 8;

    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramHandler _handler;
    private readonly ILogger<TelegramLongPollingService> _logger;

    private readonly SemaphoreSlim _concurrency = new(MaxConcurrency, MaxConcurrency);
    private readonly Dictionary<long, (Task Tail, int Pending)> _chatQueues = new();
    private readonly Lock _chatQueuesLock = new();

    public TelegramLongPollingService(
        ITelegramBotClient botClient,
        ITelegramHandler handler,
        ILogger<TelegramLongPollingService> logger)
    {
        _botClient = botClient;
        _logger = logger;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [],
            DropPendingUpdates = true,
        };

        _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, stoppingToken);

        // Держим BackgroundService живым, пока приложение не остановят
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static long GetChatId(Update update)
        => update.Message?.Chat.Id
           ?? update.CallbackQuery?.Message?.Chat.Id
           ?? update.CallbackQuery?.From.Id
           ?? 0;

    /// <summary>
    /// Ставит обновление в очередь его чата и сразу возвращает управление приёму обновлений:
    /// медленный ответ VPN-сервера одному пользователю не задерживает остальных.
    /// Обновления одного чата обрабатываются строго по порядку — от этого зависят текстовые сценарии
    /// (ввод имени соединения, проверка лимита)
    /// </summary>
    private Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var chatId = GetChatId(update);

        lock (_chatQueuesLock)
        {
            var (previous, pending) = _chatQueues.GetValueOrDefault(chatId, (Task.CompletedTask, 0));
            var current = ProcessAfterAsync(previous, chatId, update, cancellationToken);
            _chatQueues[chatId] = (current, pending + 1);
        }

        return Task.CompletedTask;
    }

    private async Task ProcessAfterAsync(Task previous, long chatId, Update update, CancellationToken cancellationToken)
    {
        // Уходим из-под блокировки, в которой задачу поставили в очередь
        await Task.Yield();

        // Предыдущая задача не бросает исключений — все ошибки ловятся ниже
        await previous;

        try
        {
            await _concurrency.WaitAsync(cancellationToken);
            try
            {
                await _handler.HandleAsync(update, cancellationToken);
            }
            finally
            {
                _concurrency.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while handling update");
        }
        finally
        {
            lock (_chatQueuesLock)
            {
                // Убираем очередь чата, если за нами никто не встал
                var (tail, pending) = _chatQueues[chatId];
                if (pending <= 1)
                {
                    _chatQueues.Remove(chatId);
                }
                else
                {
                    _chatQueues[chatId] = (tail, pending - 1);
                }
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        var msg = exception switch
        {
            ApiRequestException apiEx => $"Telegram API error [{apiEx.ErrorCode}]: {apiEx.Message}",
            _ => exception.Message
        };

        _logger.LogWarning(exception, "Polling error: {Message}", msg);
        return Task.CompletedTask;
    }
}