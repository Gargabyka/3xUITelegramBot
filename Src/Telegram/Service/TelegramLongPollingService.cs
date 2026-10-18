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
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramHandler _handler;
    private readonly ILogger<TelegramLongPollingService> _logger;

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

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        try
        {
            await _handler.HandleAsync(update, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while handling update");
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