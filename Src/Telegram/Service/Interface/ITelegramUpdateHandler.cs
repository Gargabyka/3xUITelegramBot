using Telegram.Bot.Types;

namespace Telegram.Service.Interface;

/// <summary>
/// Обработчик Telegram сообщений
/// </summary>
public interface ITelegramUpdateHandler
{
    /// <summary>
    /// Обрабатывает входящее сообщение
    /// </summary>
    /// <param name="update">Входящее сообщение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task UpdateHandlerAsync(Update update, CancellationToken cancellationToken);
}