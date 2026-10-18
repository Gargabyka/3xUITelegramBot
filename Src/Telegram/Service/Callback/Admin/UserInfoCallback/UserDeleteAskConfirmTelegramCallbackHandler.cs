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
public sealed class UserDeleteAskConfirmTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly TelegramOptions _telegramOptions;

	public UserDeleteAskConfirmTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
		_telegramOptions = telegramOptions.Value;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserDeleteAskConfirm;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var telegramId = LongGuidCodec.Decode(request.Id);

		// Защита: пользователя из AdminIds нельзя удалить "другим пользователям"
		if (_telegramOptions.AdminIds.Contains(telegramId) && chatId != telegramId)
		{
			var backToUserInfo = new TelegramRequest(
				Id: request.Id,
				CallbackEvent: TelegramCallbackEvent.UserInfo,
				SourceEvent: TelegramCallbackEvent.UserDeleteAskConfirm);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Нельзя удалить пользователя из списка AdminIds.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToUserInfo))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		using var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

		var user = await repo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
		if (user is null)
		{
			var backMissing = BackTo(TelegramCallbackEvent.UserList, sourceEvent: TelegramCallbackEvent.AdminPanel);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Пользователь не найден (возможно, уже удалён).",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backMissing))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var confirmRequest = WithEvent(request, TelegramCallbackEvent.UserDeleteConfirm, sourceEvent: TelegramCallbackEvent.UserDeleteAskConfirm);
		var backToUserInfoRequest = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserInfo,
			SourceEvent: TelegramCallbackEvent.UserDeleteAskConfirm);

		var markup = new InlineKeyboardMarkup(
		[
			[
				InlineKeyboardButton.WithCallbackData("✅ Да, удалить", Encode(confirmRequest))
			],
			[
				InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToUserInfoRequest))
			]
		]);

		await _bot.SendMessage(
			chatId: chatId,
			text: "Точно удалить этого пользователя? Действие необратимо.",
			replyMarkup: markup,
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}