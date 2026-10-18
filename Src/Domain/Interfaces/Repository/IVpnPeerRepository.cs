using Domain.Entities;

namespace Domain.Interfaces.Repository;

public interface IVpnPeerRepository
{
	/// <summary>
	/// Добавить соединения
	/// </summary>
	/// <param name="peer">Соединение</param>
	public void Add(VpnPeer peer);

	/// <summary>
	/// Добавить список соединений
	/// </summary>
	/// <param name="peers">Соединения</param>
	public void AddRange(VpnPeer[] peers);

	/// <summary>
	/// Удалить соединение
	/// </summary>
	/// <param name="peer">Соединение</param>
	public void Remove(VpnPeer peer);

	/// <summary>
	/// Получить информацию о соединениях
	/// </summary>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Возвращает список соединений</returns>
	public Task<VpnPeer[]> GetPeersAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Найти соединение по идентификатору клиента 3x-ui.
	/// </summary>
	public Task<VpnPeer?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken);
}