using Telegram.Bot.Types;

namespace Telegram.Service.Interface;

/// <summary>
/// Обработчик текстовых сообщений для пошаговых сценариев (wizard/state machine).
/// Возвращает true, если сообщение обработано и дальнейшая обработка не нужна.
/// </summary>
public interface ITelegramTextFlowHandler
{
    Task<bool> TryHandleAsync(Update update, CancellationToken cancellationToken);
}