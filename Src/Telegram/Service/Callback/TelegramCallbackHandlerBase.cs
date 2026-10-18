using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;

namespace Telegram.Service.Callback;

public abstract class TelegramCallbackHandlerBase : ITelegramCallbackHandler
{
    private readonly ITelegramBotClient _telegramBot;

    protected TelegramCallbackHandlerBase(ITelegramBotClient telegramBot)
    {
        _telegramBot = telegramBot;
    }

    public abstract TelegramCallbackEvent CallbackEvent { get; }

    public abstract Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken);

    protected static (long ChatId, int MessageId, string CallbackQueryId) GetCallbackContext(Update update)
    {
        var callbackQuery = update.CallbackQuery ?? throw new InvalidOperationException("Update.CallbackQuery is null");
        var message = callbackQuery.Message ?? throw new InvalidOperationException("CallbackQuery.Message is null");

        return (message.Chat.Id, message.MessageId, callbackQuery.Id);
    }

    /// <summary>
    /// Кнопка "Назад" возвращает пользователя на предыдущий экран
    /// </summary>
    /// <param name="callbackEvent">Событие перехода</param>
    /// <param name="sourceEvent">Исходное событие</param>
    /// <returns>Запрос обработки телеграм</returns>
    protected static TelegramRequest BackTo(TelegramCallbackEvent callbackEvent, TelegramCallbackEvent? sourceEvent = null)
        => new(Guid.Empty, callbackEvent, sourceEvent ?? callbackEvent);

    /// <summary>
    /// Кнопка для создания нового события
    /// </summary>
    /// <param name="request">Запрос</param>
    /// <param name="callbackEvent">Событие перехода</param>
    /// <param name="sourceEvent">Исходное событие</param>
    /// <returns>Запрос обработки телеграм</returns>
    protected static TelegramRequest WithEvent(TelegramRequest request, TelegramCallbackEvent callbackEvent, TelegramCallbackEvent? sourceEvent = null)
        => request with { CallbackEvent = callbackEvent, SourceEvent = sourceEvent ?? request.SourceEvent };

    protected static string Encode(TelegramRequest request)
        => TelegramCallbackDataCodec.Encode(request);

    /// <summary>
    /// Попытка удалить сообщение откуда пришли
    /// </summary>
    /// <param name="update">Событие</param>
    /// <param name="cancellationToken">Токен отмены</param>
    protected async Task TryDeleteOriginMessageAsync(Update update, CancellationToken cancellationToken)
    {
        var (chatId, messageId, _) = GetCallbackContext(update);

        try
        {
            await _telegramBot.DeleteMessage(chatId, messageId, cancellationToken);
        }
        catch
        {
            // Если удалить нельзя — не валим обработчик.
        }
    }

    /// <summary>
    /// Возвращаем ответ callback query
    /// </summary>
    /// <param name="update">Событие</param>
    /// <param name="cancellationToken">Токен отмены</param>
    protected async Task AnswerAsync(Update update, CancellationToken cancellationToken)
    {
        var (_, _, callbackQueryId) = GetCallbackContext(update);
        await _telegramBot.AnswerCallbackQuery(callbackQueryId, cancellationToken: cancellationToken);
    }
}