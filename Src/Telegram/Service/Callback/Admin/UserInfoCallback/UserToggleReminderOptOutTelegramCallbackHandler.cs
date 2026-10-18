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
public sealed class UserToggleReminderOptOutTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;

	public UserToggleReminderOptOutTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserToggleReminderOptOut;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var telegramId = LongGuidCodec.Decode(request.Id);

		var backToUserInfo = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserInfo,
			SourceEvent: TelegramCallbackEvent.UserToggleReminderOptOut);

		using var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
		var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

		var user = await repo.GetUserByTelegramIdForUpdateAsync(telegramId, cancellationToken);
		if (user is null)
		{
			await _bot.SendMessage(
				chatId: chatId,
				text: "Пользователь не найден.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToUserInfo))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		user.ReminderOptOut = !user.ReminderOptOut;
		await uow.SaveChangesAsync(cancellationToken);

		var statusText = user.ReminderOptOut
			? "Напоминания об оплате хостинга для этого пользователя отключены."
			: "Напоминания об оплате хостинга для этого пользователя включены.";

		await _bot.SendMessage(
			chatId: chatId,
			text: statusText,
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(backToUserInfo))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
