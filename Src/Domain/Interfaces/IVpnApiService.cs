using Domain.Enums;
using Domain.Models;

namespace Domain.Interfaces;

/// <summary>
/// Единая точка работы со всеми настроенными VPN-серверами
/// </summary>
public interface IVpnApiService
{
	/// <summary>
	/// Профили подключения, доступные для создания соединения (в порядке настройки)
	/// </summary>
	public IReadOnlyList<VpnProfile> AvailableProfiles { get; }

	/// <summary>
	/// Состояние каждого сервера: доступность и его клиенты
	/// </summary>
	public Task<VpnBackendStatus[]> GetStatusAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Клиенты всех серверов. Недоступный сервер пропускается (пишется в лог)
	/// </summary>
	public Task<VpnServerClient[]> GetClients(CancellationToken cancellationToken);

	/// <summary>
	/// Конфигурация клиента (сервер определяется по идентификатору)
	/// </summary>
	public Task<byte[]> DownloadConfigAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Удалить клиента (сервер определяется по идентификатору)
	/// </summary>
	public Task DeleteClientAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Добавить клиента в выбранный профиль
	/// </summary>
	public Task AddClientAsync(VpnProfileType profile, string name, CancellationToken cancellationToken);

	/// <summary>
	/// Включить клиента (сервер определяется по идентификатору)
	/// </summary>
	public Task EnableClientAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Отключить клиента (сервер определяется по идентификатору)
	/// </summary>
	public Task DisableClientAsync(Guid id, CancellationToken cancellationToken);
}
