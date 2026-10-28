namespace Domain.Enums;

/// <summary>
/// Профиль подключения — вариант, который выбирается при создании соединения
/// </summary>
public enum VpnProfileType
{
	/// <summary>
	/// VLESS + Reality (raw/tcp), 3x-ui
	/// </summary>
	VlessRaw = 0,

	/// <summary>
	/// VLESS + XHTTP, 3x-ui
	/// </summary>
	VlessXhttp = 1,

	/// <summary>
	/// VLESS + XHTTP через CDN, 3x-ui
	/// </summary>
	VlessXhttpCdn = 2,

	/// <summary>
	/// AmneziaWG, amnezia-wg-easy
	/// </summary>
	AmneziaWg = 3,
}
