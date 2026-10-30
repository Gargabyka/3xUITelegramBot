using Domain.Extensions;
using Domain.Interfaces;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Ссылка-подписка соединения: у каждого VLESS-соединения свой subId, и сервер подписок 3x-ui
/// отдаёт по ссылке только это соединение. AmneziaWG подписок не поддерживает
/// </summary>
public sealed class SubscriptionService
{
	private readonly IVpnApiService _vpnApiService;

	public SubscriptionService(IVpnApiService vpnApiService)
	{
		_vpnApiService = vpnApiService;
	}

	/// <summary>
	/// subId соединения для ссылки-подписки. Если subId пустой или общий с другим клиентом
	/// (например, остался от прежней подписки «одна на пользователя» или так настроено в панели),
	/// соединению выдаётся собственный — иначе по его ссылке пришли бы и чужие соединения
	/// </summary>
	/// <param name="clientId">Идентификатор клиента на VPN-сервере</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>subId или null, если клиент не найден или его сервер не поддерживает подписки</returns>
	public async Task<string?> GetClientSubscriptionIdAsync(Guid clientId, CancellationToken cancellationToken)
	{
		var clients = await _vpnApiService.GetClients(cancellationToken);
		var client = clients.FirstOrDefault(x => x.Id == clientId);
		if (client is null || !client.Profile.IsVless())
		{
			return null;
		}

		var current = client.SubscriptionId;
		var isOwn = !string.IsNullOrEmpty(current)
		            && !clients.Any(x => x.Id != clientId && x.SubscriptionId == current);
		if (isOwn)
		{
			return current;
		}

		var subscriptionId = SubscriptionIds.New();
		await _vpnApiService.SetSubscriptionIdAsync(clientId, subscriptionId, cancellationToken);
		return subscriptionId;
	}
}
