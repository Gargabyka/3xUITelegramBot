using Telegram.Bot.Types;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Interface;

/// <summary>
/// Обработчик обратных вызовов телеграм
/// </summary>
public interface ITelegramCallbackHandler
{
    /// <summary>
    /// Возвращаемое событие
    /// </summary>
    public TelegramCallbackEvent CallbackEvent { get; }

    /// <summary>
    /// Обработчик возвращаемого события
    /// </summary>
    /// <param name="update">Событие</param>
    /// <param name="request">Запрос</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken);
}