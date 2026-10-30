namespace Telegram.Models;

/// <summary>
/// Ссылка-подписка через сервер подписок 3x-ui
/// </summary>
public sealed class SubscriptionOptions
{
	/// <summary>
	/// Начало ссылки подписки без subId: адрес сервера подписок 3x-ui и путь,
	/// например https://vpn.example.com:2096/sub/ (Settings → Subscription в панели).
	/// Пусто — подписки выключены, кнопки в боте нет
	/// </summary>
	public string? BaseUrl { get; init; }

	/// <summary>
	/// Подписки настроены
	/// </summary>
	public bool Enabled => !string.IsNullOrWhiteSpace(BaseUrl);

	/// <summary>
	/// Ссылка-подписка пользователя
	/// </summary>
	public string BuildUrl(string subscriptionId) => $"{BaseUrl!.TrimEnd('/')}/{subscriptionId}";
}
