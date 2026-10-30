using Domain.Models;

namespace Domain.Interfaces;

/// <summary>
/// VPN-сервер одного протокола (3x-ui, amnezia-wg-easy)
/// </summary>
public interface IVpnBackend
{
	/// <summary>
	/// Профиль подключения, который обслуживает сервер
	/// </summary>
	public VpnProfile Profile { get; }

	/// <summary>
	/// Сервер отдаёт клиентов по ссылке-подписке (subId)
	/// </summary>
	public bool SupportsSubscription { get; }

	/// <summary>
	/// Получить клиентов сервера
	/// </summary>
	public Task<VpnServerClient[]> GetClients(CancellationToken cancellationToken);

	/// <summary>
	/// Получить конфигурацию клиента (vless-ссылка или .conf)
	/// </summary>
	public Task<byte[]> DownloadConfigAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Удалить клиента
	/// </summary>
	public Task DeleteClientAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Добавить клиента
	/// </summary>
	/// <param name="name">Имя клиента</param>
	/// <param name="subscriptionId">Подписка, в которую попадёт клиент (null — своя случайная); сервер без подписок её игнорирует</param>
	/// <param name="cancellationToken">Токен отмены</param>
	public Task AddClientAsync(string name, string? subscriptionId, CancellationToken cancellationToken);

	/// <summary>
	/// Включить клиента
	/// </summary>
	public Task EnableClientAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Отключить клиента
	/// </summary>
	public Task DisableClientAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Переименовать клиента (ключи и подключение не меняются)
	/// </summary>
	public Task RenameClientAsync(Guid id, string newName, CancellationToken cancellationToken);

	/// <summary>
	/// Перенести клиента в подписку (сменить subId). Ничего не делает, если клиент уже в ней
	/// </summary>
	public Task SetSubscriptionIdAsync(Guid id, string subscriptionId, CancellationToken cancellationToken);
}