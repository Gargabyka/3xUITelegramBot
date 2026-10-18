using Telegram.Enums;

namespace Telegram.Models;

/// <summary>
/// Запрос обработки телеграм
/// </summary>
/// <param name="Id">Идентификатор пользователя</param>
/// <param name="CallbackEvent">Событие</param>
/// <param name="SourceEvent">Откуда пользователь пришёл (куда возвращаться по "Назад")</param>
/// <param name="Page">Номер страницы (для пагинации). По умолчанию 0</param>
public record TelegramRequest(Guid Id, TelegramCallbackEvent CallbackEvent, TelegramCallbackEvent? SourceEvent, int Page = 0);