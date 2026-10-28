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
	public Task AddClientAsync(string name, CancellationToken cancellationToken);

	/// <summary>
	/// Включить клиента
	/// </summary>
	public Task EnableClientAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Отключить клиента
	/// </summary>
	public Task DisableClientAsync(Guid id, CancellationToken cancellationToken);
}