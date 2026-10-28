namespace XUi.Models.Options;

/// <summary>
/// Настройки доступа к панели 3x-ui
/// </summary>
public sealed class XUiOptions
{
	/// <summary>
	/// Адрес панели вместе с webBasePath, например http://3x-ui:2053/secret/
	/// </summary>
	public required string ApiUrl { get; init; }

	/// <summary>
	/// API-токен (Settings → Security → API Token)
	/// </summary>
	public required string ApiToken { get; init; }

	/// <summary>
	/// XTLS flow новых клиентов по умолчанию (для VLESS + Reality на raw обычно xtls-rprx-vision)
	/// </summary>
	public string Flow { get; init; } = "xtls-rprx-vision";

	/// <summary>
	/// Игнорировать ошибки TLS-сертификата панели (самоподписанный сертификат)
	/// </summary>
	public bool AllowInvalidCertificate { get; init; }

	// Профили подключения (XUi:Inbounds) читаются отдельно при регистрации сервисов:
	// inbound'ы с пустым Id пропускаются, а привязка пустой строки к int здесь уронила бы все настройки
}
