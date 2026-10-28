namespace AmneziaWg.Models;

/// <summary>
/// Настройки доступа к веб-интерфейсу amnezia-wg-easy
/// </summary>
public sealed class AmneziaWgOptions
{
	/// <summary>
	/// Включить подключения через amnezia-wg-easy
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	/// Адрес веб-интерфейса, например http://amnezia-wg-easy:51821/
	/// </summary>
	public string ApiUrl { get; init; } = string.Empty;

	/// <summary>
	/// Пароль от веб-интерфейса (PASSWORD в настройках контейнера)
	/// </summary>
	public string ApiPassword { get; init; } = string.Empty;
}
