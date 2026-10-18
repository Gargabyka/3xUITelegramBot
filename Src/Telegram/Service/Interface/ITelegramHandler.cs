using Telegram.Bot.Types;

namespace Telegram.Service.Interface;

/// <summary>
/// Обработчик Telegram
/// </summary>
public interface ITelegramHandler
{
    /// <summary>
    /// Обработчик
    /// </summary>
    /// <param name="update">Входящее сообщение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task HandleAsync(Update update, CancellationToken cancellationToken);
}