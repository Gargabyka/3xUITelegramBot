using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service;

/// <summary>
/// Возврат из карточки соединения в список, откуда её открыли
/// </summary>
public sealed class ConnectionNavigation
{
	private readonly IServiceScopeFactory _scopeFactory;

	public ConnectionNavigation(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	/// <summary>
	/// Запрос для кнопки «Назад» к списку соединений.
	/// В данные кнопки помещается один идентификатор, и в карточке это идентификатор соединения,
	/// поэтому пользователя, чьи соединения смотрел админ, определяем по владельцу соединения.
	/// После удаления соединения владельца уже не найти — вызывать до удаления
	/// </summary>
	/// <param name="cardRequest">Запрос карточки соединения (Id — идентификатор клиента)</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Запрос перехода к списку</returns>
	public async Task<TelegramRequest> BackToListAsync(TelegramRequest cardRequest, CancellationToken cancellationToken)
	{
		switch (cardRequest.SourceEvent)
		{
			case TelegramCallbackEvent.MyConnections:
				return new TelegramRequest(Guid.Empty, TelegramCallbackEvent.MyConnections, TelegramCallbackEvent.MyConnections, cardRequest.Page);

			case TelegramCallbackEvent.UserConnections:
				using (var scope = _scopeFactory.CreateScope())
				{
					var link = await scope.ServiceProvider
						.GetRequiredService<IVpnUserRepository>()
						.GetLinkByClientIdAsync(cardRequest.Id, cancellationToken);

					// Соединение могли отвязать, пока была открыта карточка
					return link is null
						? new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserList, TelegramCallbackEvent.AdminPanel)
						: new TelegramRequest(
							LongGuidCodec.Encode(link.Telegram.TelegramId),
							TelegramCallbackEvent.UserConnections,
							TelegramCallbackEvent.UserInfo,
							cardRequest.Page);
				}

			default:
				return new TelegramRequest(Guid.Empty, TelegramCallbackEvent.List, TelegramCallbackEvent.List, cardRequest.Page);
		}
	}
}
