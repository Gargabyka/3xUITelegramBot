using Domain.Entities;

namespace Domain.Interfaces.Repository;

public interface IVpnUserRepository
{
	/// <summary>
	/// Получить соединения пользователя (с включенным Peer).
	/// </summary>
	public Task<VpnUser[]> GetConnectionsByTelegramIdAsync(int telegramUserId, CancellationToken cancellationToken);

	/// <summary>
	/// Добавить связь пользователь↔peer.
	/// </summary>
	public void Add(VpnUser vpnUser);

	/// <summary>
	/// Проверить, что связь пользователь↔peer уже существует.
	/// </summary>
	public Task<bool> ExistsAsync(int telegramUserId, int peerId, CancellationToken cancellationToken);

	/// <summary>
	/// Получить список PeerId, которые уже привязаны к любому пользователю.
	/// </summary>
	public Task<int[]> GetUsedPeerIdsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Удалить связь между пользователем и соединением.
	/// </summary>
	public Task<int> RemoveLinkAsync(int telegramUserId, int peerId, CancellationToken cancellationToken);
}