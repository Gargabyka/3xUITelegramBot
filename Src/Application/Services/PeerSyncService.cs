using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Синхронизация локальной копии peer'ов с VPN-серверами: добавляет клиентов, созданных мимо бота,
/// обновляет имя, профиль и статус, удаляет записи о клиентах, которых больше нет на сервере
/// </summary>
public sealed class PeerSyncService
{
	private readonly IVpnApiService _vpnApiService;
	private readonly IVpnPeerRepository _peerRepository;
	private readonly IUnitOfWork _unitOfWork;

	public PeerSyncService(IVpnApiService vpnApiService, IVpnPeerRepository peerRepository, IUnitOfWork unitOfWork)
	{
		_vpnApiService = vpnApiService;
		_peerRepository = peerRepository;
		_unitOfWork = unitOfWork;
	}

	/// <summary>
	/// Выполнить синхронизацию
	/// </summary>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Отчёт о синхронизации</returns>
	public async Task<PeerSyncResult> SyncAsync(CancellationToken cancellationToken)
	{
		var statuses = await _vpnApiService.GetStatusAsync(cancellationToken);
		var available = statuses.Where(x => x.IsAvailable).ToArray();

		// Клиент, привязанный к нескольким inbound'ам, считается один раз
		var clients = available
			.SelectMany(x => x.Clients)
			.GroupBy(x => x.Id)
			.ToDictionary(x => x.Key, x => x.First());

		// Удаляем только по профилям, сервер которых ответил и вернул непустой список:
		// пустой ответ чаще означает ошибку настройки (не тот inbound), чем удаление всех клиентов,
		// а удаление peer'а каскадно рвёт его привязку к пользователю
		var removableProfiles = available
			.Where(x => x.Clients.Length > 0)
			.Select(x => x.Profile.Type)
			.ToHashSet();

		var peers = await _peerRepository.GetPeersForUpdateAsync(cancellationToken);
		var knownIds = peers.Select(x => x.ClientId).ToHashSet();

		var updated = 0;
		var removed = 0;

		foreach (var peer in peers)
		{
			if (clients.TryGetValue(peer.ClientId, out var client))
			{
				if (peer.Name != client.Name || peer.IsEnabled != client.Enabled || peer.Profile != client.Profile)
				{
					peer.Name = client.Name;
					peer.IsEnabled = client.Enabled;
					peer.Profile = client.Profile;
					updated++;
				}
			}
			else if (removableProfiles.Contains(peer.Profile))
			{
				_peerRepository.Remove(peer);
				removed++;
			}
		}

		var peersToAdd = clients.Values
			.Where(x => !knownIds.Contains(x.Id))
			.Select(ToPeer)
			.ToArray();

		if (peersToAdd.Length > 0)
		{
			_peerRepository.AddRange(peersToAdd);
		}

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		var unavailable = statuses
			.Where(x => !x.IsAvailable)
			.Select(x => x.Profile.Title)
			.ToArray();

		return new PeerSyncResult(clients.Count, peersToAdd.Length, updated, removed, unavailable);
	}

	private static VpnPeer ToPeer(VpnServerClient client)
		=> new()
		{
			ClientId = client.Id,
			Name = client.Name,
			Profile = client.Profile,
			IsEnabled = client.Enabled,
			Address = client.Address,
			PublicKey = client.PublicKey,
			CreatedAt = DateOnly.FromDateTime(client.CreatedAt.UtcDateTime),
		};
}

/// <summary>
/// Отчёт о синхронизации peer'ов
/// </summary>
/// <param name="Total">Клиентов на доступных серверах</param>
/// <param name="Added">Добавлено в БД</param>
/// <param name="Updated">Обновлено (имя, профиль или статус)</param>
/// <param name="Removed">Удалено из БД (клиента больше нет на сервере)</param>
/// <param name="UnavailableProfiles">Профили, сервер которых не ответил (по ним ничего не удалялось)</param>
public sealed record PeerSyncResult(int Total, int Added, int Updated, int Removed, string[] UnavailableProfiles);
