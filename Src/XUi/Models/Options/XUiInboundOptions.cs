using Domain.Enums;

namespace XUi.Models.Options;

/// <summary>
/// Профиль подключения VLESS — один inbound панели 3x-ui
/// </summary>
public sealed class XUiInboundOptions
{
	/// <summary>
	/// Идентификатор inbound'а (колонка ID в панели)
	/// </summary>
	public int Id { get; init; }

	/// <summary>
	/// Профиль, который представляет этот inbound: VlessRaw, VlessXhttp или VlessXhttpCdn
	/// </summary>
	public VpnProfileType Profile { get; init; }

	/// <summary>
	/// Порт, который подставляется в ссылку и QR вместо порта inbound'а
	/// (например, 443 для inbound'а за CDN). Пусто — порт не меняется
	/// </summary>
	public int? Port { get; init; }

	/// <summary>
	/// XTLS flow клиентов этого профиля. Пусто (null) — берётся XUi:Flow,
	/// пустая строка — без flow (нужно для XHTTP)
	/// </summary>
	public string? Flow { get; init; }
}
