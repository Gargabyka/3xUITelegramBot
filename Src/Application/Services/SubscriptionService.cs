using Domain.Entities;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Ссылка-подписка: у пользователя один subId, и все его VLESS-соединения в 3x-ui несут этот subId —
/// сервер подписок 3x-ui отдаёт их одной ссылкой. AmneziaWG в подписку не входит
/// </summary>
public sealed class SubscriptionService
{
	private readonly IVpnApiService _vpnApiService;
	private readonly ITelegramUserRepository _telegramUserRepository;
	private readonly IVpnUserRepository _vpnUserRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ILogger<SubscriptionService> _logger;

	public SubscriptionService(
		IVpnApiService vpnApiService,
		ITelegramUserRepository telegramUserRepository,
		IVpnUserRepository vpnUserRepository,
		IUnitOfWork unitOfWork,
		ILogger<SubscriptionService> logger)
	{
		_vpnApiService = vpnApiService;
		_telegramUserRepository = telegramUserRepository;
		_vpnUserRepository = vpnUserRepository;
		_unitOfWork = unitOfWork;
		_logger = logger;
	}

	/// <summary>
	/// subId пользователя; создаётся при первом обращении
	/// </summary>
	/// <param name="telegramId">Telegram ID пользователя</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>subId или null, если пользователя нет в боте</returns>
	public async Task<string?> GetOrCreateIdAsync(long telegramId, CancellationToken cancellationToken)
	{
		var user = await _telegramUserRepository.GetUserByTelegramIdForUpdateAsync(telegramId, cancellationToken);
		if (user is null)
		{
			return null;
		}

		if (user.SubscriptionId is null)
		{
			user.SubscriptionId = SubscriptionIds.New();
			await _unitOfWork.SaveChangesAsync(cancellationToken);
		}

		return user.SubscriptionId;
	}

	/// <summary>
	/// Привести все VLESS-соединения пользователя к его подписке (например, после привязки соединения админом)
	/// </summary>
	/// <param name="telegramId">Telegram ID пользователя</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Результат или null, если пользователя нет в боте</returns>
	public async Task<SubscriptionSyncResult?> SyncAsync(long telegramId, CancellationToken cancellationToken)
	{
		var subscriptionId = await GetOrCreateIdAsync(telegramId, cancellationToken);
		if (subscriptionId is null)
		{
			return null;
		}

		var peers = await GetVlessPeersAsync(telegramId, cancellationToken);

		// Текущие subId берём из общего списка клиентов: менять нужно только те, что ещё не в подписке
		var currentIds = (await _vpnApiService.GetClients(cancellationToken))
			.ToDictionary(x => x.Id, x => x.SubscriptionId);

		var failed = 0;
		foreach (var peer in peers)
		{
			if (currentIds.TryGetValue(peer.ClientId, out var current) && current == subscriptionId)
			{
				continue;
			}

			try
			{
				await _vpnApiService.SetSubscriptionIdAsync(peer.ClientId, subscriptionId, cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Одно недоступное или удалённое соединение не должно ломать подписку остальных
				failed++;
				_logger.LogWarning(ex, "Не удалось добавить клиента {ClientId} в подписку пользователя {TelegramId}", peer.ClientId, telegramId);
			}
		}

		return new SubscriptionSyncResult(subscriptionId, peers.Length, failed);
	}

	/// <summary>
	/// Добавить только что созданное соединение в подписку пользователя.
	/// Ошибка только пишется в лог: соединение уже создано, а в подписку оно попадёт при следующем открытии «Подписки»
	/// </summary>
	/// <param name="telegramId">Telegram ID владельца</param>
	/// <param name="clientId">Идентификатор клиента на VPN-сервере</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Задача</returns>
	public async Task AttachAsync(long telegramId, Guid clientId, CancellationToken cancellationToken)
	{
		try
		{
			var subscriptionId = await GetOrCreateIdAsync(telegramId, cancellationToken);
			if (subscriptionId is not null)
			{
				await _vpnApiService.SetSubscriptionIdAsync(clientId, subscriptionId, cancellationToken);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Не удалось добавить клиента {ClientId} в подписку пользователя {TelegramId}", clientId, telegramId);
		}
	}

	/// <summary>
	/// Вывести соединение из подписки (после отвязки): новый случайный subId, чтобы оно не осталось в ссылке прежнего владельца.
	/// Ошибка сервера только пишется в лог: отвязка в боте уже выполнена и откатываться не должна
	/// </summary>
	/// <param name="clientId">Идентификатор клиента на VPN-сервере</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>true — соединение выведено из подписки</returns>
	public async Task<bool> DetachAsync(Guid clientId, CancellationToken cancellationToken)
	{
		try
		{
			await _vpnApiService.SetSubscriptionIdAsync(clientId, SubscriptionIds.New(), cancellationToken);
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Не удалось вывести клиента {ClientId} из подписки", clientId);
			return false;
		}
	}

	/// <summary>
	/// Вывести из подписки все соединения пользователя (перед его удалением): старая ссылка перестанет их отдавать
	/// </summary>
	/// <param name="telegramId">Telegram ID пользователя</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Задача</returns>
	public async Task DetachAllAsync(long telegramId, CancellationToken cancellationToken)
	{
		foreach (var peer in await GetVlessPeersAsync(telegramId, cancellationToken))
		{
			await DetachAsync(peer.ClientId, cancellationToken);
		}
	}

	private async Task<VpnPeer[]> GetVlessPeersAsync(long telegramId, CancellationToken cancellationToken)
	{
		var user = await _telegramUserRepository.GetUserByTelegramIdAsync(telegramId, cancellationToken);
		if (user is null)
		{
			return [];
		}

		var connections = await _vpnUserRepository.GetConnectionsByTelegramIdAsync(user.Id, cancellationToken);
		return connections
			.Select(x => x.Peer)
			.Where(x => x.Profile.IsVless())
			.ToArray();
	}
}

/// <summary>
/// Результат синхронизации подписки
/// </summary>
/// <param name="SubscriptionId">subId пользователя</param>
/// <param name="VlessConnections">Сколько VLESS-соединений у пользователя</param>
/// <param name="Failed">Скольких не удалось добавить в подписку</param>
public sealed record SubscriptionSyncResult(string SubscriptionId, int VlessConnections, int Failed);
