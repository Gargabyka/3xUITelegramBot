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
    /// Идентификатор VLESS + Reality inbound, к которому привязываются клиенты
    /// </summary>
	public required int InboundId { get; init; }

    /// <summary>
    /// XTLS flow для новых клиентов (для Reality обычно xtls-rprx-vision)
    /// </summary>
	public string Flow { get; init; } = "xtls-rprx-vision";

    /// <summary>
    /// Игнорировать ошибки TLS-сертификата панели (самоподписанный сертификат)
    /// </summary>
	public bool AllowInvalidCertificate { get; init; }
}