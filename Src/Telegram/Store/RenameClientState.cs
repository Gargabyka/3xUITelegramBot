using Telegram.Enums;

namespace Telegram.Store;

/// <summary>
/// Сценарий переименования: какое соединение и куда вернуться после
/// </summary>
/// <param name="ClientId">Идентификатор клиента на VPN-сервере</param>
/// <param name="SourceEvent">Откуда открыта карточка (MyConnections или List)</param>
/// <param name="Page">Страница списка</param>
public sealed record RenameClientState(Guid ClientId, TelegramCallbackEvent? SourceEvent, int Page);
