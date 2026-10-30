using Domain.Enums;

namespace Domain.Models;

/// <summary>
/// Клиент VPN-сервера (3x-ui или amnezia-wg-easy)
/// </summary>
/// <param name="Id">Идентификатор клиента на сервере</param>
/// <param name="Profile">Профиль подключения</param>
/// <param name="Name">Имя клиента</param>
/// <param name="Enabled">Признак, что клиент включен</param>
/// <param name="Address">IP-адрес</param>
/// <param name="PublicKey">Публичный ключ</param>
/// <param name="CreatedAt">Дата создания</param>
/// <param name="LatestHandshakeAt">Дата последнего подключения (null — ни разу не подключался)</param>
/// <param name="TransferRx">Байт получено сервером от клиента</param>
/// <param name="TransferTx">Байт отправлено сервером клиенту</param>
/// <param name="SubscriptionId">subId ссылки-подписки (только 3x-ui; null — у сервера нет подписок)</param>
public record VpnServerClient(
	Guid Id,
	VpnProfileType Profile,
	string Name,
	bool Enabled,
	string Address,
	string PublicKey,
	DateTimeOffset CreatedAt,
	DateTimeOffset? LatestHandshakeAt,
	long TransferRx,
	long TransferTx,
	string? SubscriptionId = null);
