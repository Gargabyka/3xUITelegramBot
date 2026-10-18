namespace Telegram.Models;

/// <summary>
/// Настройки для телеграм бота
/// </summary>
public sealed class TelegramOptions
{
    /// <summary>
    /// Токен бота
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Идентификаторы админов
    /// </summary>
    public required long[] AdminIds { get; init; }

    /// <summary>
    /// Лимит соединений по умолчанию для новых пользователей (null — без ограничений).
    /// На уже существующих пользователей не влияет — им лимит не выставляется задним числом.
    /// </summary>
    public int? DefaultConnectionLimit { get; init; }
}