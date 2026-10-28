namespace Telegram.Models;

/// <summary>
/// Настройки мониторинга VPN-серверов
/// </summary>
public sealed class MonitoringOptions
{
	/// <summary>
	/// Включить проверку серверов и уведомления админам
	/// </summary>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Как часто проверять серверы, минут
	/// </summary>
	public int IntervalMinutes { get; init; } = 5;

	/// <summary>
	/// Сколько проверок подряд сервер должен не отвечать, чтобы прислать уведомление
	/// (одиночный сбой сети не должен будить админов)
	/// </summary>
	public int FailureThreshold { get; init; } = 2;
}
