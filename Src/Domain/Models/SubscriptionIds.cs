namespace Domain.Models;

/// <summary>
/// Идентификаторы ссылок-подписок (subId в 3x-ui)
/// </summary>
public static class SubscriptionIds
{
	/// <summary>
	/// Случайный subId в формате, который использует сама 3x-ui
	/// </summary>
	public static string New() => Guid.NewGuid().ToString("N")[..16];
}
