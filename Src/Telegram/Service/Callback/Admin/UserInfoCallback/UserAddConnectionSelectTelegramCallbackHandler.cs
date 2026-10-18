using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin.UserInfoCallback;

[UsedImplicitly]
public sealed class UserAddConnectionSelectTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;

	public UserAddConnectionSelectTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserAddConnectionSelect;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var targetTelegramId = LongGuidCodec.Decode(request.Id);
		var peerId = request.Page;

		using var scope = _scopeFactory.CreateScope();
		var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();
		var peerRepo = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
		var telegramUserRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
		var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

		var peer = (await peerRepo.GetPeersAsync(cancellationToken)).FirstOrDefault(x => x.Id == peerId);
		if (peer is null)
		{
			var back = new TelegramRequest(request.Id, TelegramCallbackEvent.UserAddConnection, TelegramCallbackEvent.UserInfo);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Соединение не найдено.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(back))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		// Защита: если peer уже привязан к кому-то — не даём привязать повторно
		var usedPeerIds = await vpnUserRepo.GetUsedPeerIdsAsync(cancellationToken);
		if (usedPeerIds.Contains(peerId))
		{
			var back = new TelegramRequest(request.Id, TelegramCallbackEvent.UserAddConnection, TelegramCallbackEvent.UserInfo);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Это соединение уже привязано к другому пользователю.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(back))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var user = await telegramUserRepo.GetUserByTelegramIdAsync(targetTelegramId, cancellationToken);
		if (user is null)
		{
			await _bot.SendMessage(
				chatId: chatId,
				text: $"Не удалось найти пользователя: {chatId}",
				cancellationToken: cancellationToken);

			return;
		}

		vpnUserRepo.Add(new VpnUser
		{
			TelegramUserId = user.Id,
			PeerId = peerId,
		});

		await uow.SaveChangesAsync(cancellationToken);

		var toUserInfo = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserInfo,
			SourceEvent: TelegramCallbackEvent.UserAddConnectionSelect);

		await _bot.SendMessage(
			chatId: chatId,
			text: $"Соединение '{peer.Name}' добавлено пользователю.",
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(toUserInfo))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}