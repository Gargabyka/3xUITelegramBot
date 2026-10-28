namespace Telegram.Store;

/// <summary>
/// Сценарий рассылки
/// </summary>
/// <param name="MessageId">Сообщение администратора для рассылки; null — ещё ждём сообщение</param>
public sealed record BroadcastState(int? MessageId);
