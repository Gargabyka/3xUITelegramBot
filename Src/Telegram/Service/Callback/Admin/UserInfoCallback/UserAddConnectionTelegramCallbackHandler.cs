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
public sealed class UserAddConnectionTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private const int PageSize = 9;

	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;

	public UserAddConnectionTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserAddConnection;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var targetTelegramId = LongGuidCodec.Decode(request.Id);

		using var scope = _scopeFactory.CreateScope();
		var tgRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
		var peerRepo = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
		var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();

		var user = await tgRepo.GetUserByTelegramIdAsync(targetTelegramId, cancellationToken);
		if (user is null)
		{
			var backMissing = BackTo(TelegramCallbackEvent.UserList, sourceEvent: TelegramCallbackEvent.AdminPanel);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Пользователь не найден.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backMissing))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var allPeers = await peerRepo.GetPeersAsync(cancellationToken);
		var usedPeerIds = await vpnUserRepo.GetUsedPeerIdsAsync(cancellationToken);
		var used = usedPeerIds.ToHashSet();

		var available = allPeers
			.Where(p => !used.Contains(p.Id))
			.OrderByDescending(p => p.CreatedAt)
			.ToArray();

		if (available.Length == 0)
		{
			var backToUser = new TelegramRequest(
				Id: request.Id,
				CallbackEvent: TelegramCallbackEvent.UserInfo,
				SourceEvent: TelegramCallbackEvent.UserAddConnection);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Нет доступных соединений (все уже привязаны к пользователям).",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(backToUser))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var totalPages = (int)Math.Ceiling(available.Length / (double)PageSize);
		var page = request.Page;

		if (page < 0)
		{
			page = 0;
		}

		if (page > totalPages - 1)
		{
			page = totalPages - 1;
		}

		var pagePeers = available
			.Skip(page * PageSize)
			.Take(PageSize)
			.ToArray();

		var rows = new List<InlineKeyboardButton[]>();

		// кнопки peer’ов (по 3 в ряд)
		for (var i = 0; i < pagePeers.Length; i += 3)
		{
			var chunk = pagePeers.Skip(i).Take(3).ToArray();
			rows.Add(
				chunk.Select(p =>
						InlineKeyboardButton.WithCallbackData(
							text: p.Name,
							callbackData: Encode(
								new TelegramRequest(
									Id: request.Id, // целевой пользователь
									CallbackEvent: TelegramCallbackEvent.UserAddConnectionSelect,
									SourceEvent: TelegramCallbackEvent.UserAddConnection,
									Page: p.Id)))) // PeerId
					.ToArray());
		}

		// навигация
		var prevPage = Math.Max(0, page - 1);
		var nextPage = Math.Min(totalPages - 1, page + 1);

		var navPrev = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserAddConnection, TelegramCallbackEvent.UserInfo, prevPage) with { Id = request.Id };
		var navNext = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserAddConnection, TelegramCallbackEvent.UserInfo, nextPage) with { Id = request.Id };

		rows.Add([
			InlineKeyboardButton.WithCallbackData(page > 0 ? "⬅️" : " ", Encode(navPrev)),
			InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", Encode(new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserAddConnection, TelegramCallbackEvent.UserInfo, page) with { Id = request.Id })),
			InlineKeyboardButton.WithCallbackData(page < totalPages - 1 ? "➡️" : " ", Encode(navNext)),
		]);

		var back = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserInfo,
			SourceEvent: TelegramCallbackEvent.UserAddConnection);

		rows.Add([InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(back))]);

		await _bot.SendMessage(
			chatId: chatId,
			text: "Выберите соединение, которое нужно привязать к пользователю:",
			replyMarkup: new InlineKeyboardMarkup(rows),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}