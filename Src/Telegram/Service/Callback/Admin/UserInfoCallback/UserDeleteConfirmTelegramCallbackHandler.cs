using Application.Services;
using Domain.Enums;
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
public sealed class UserDeleteConfirmTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly AuditLogger _audit;
	private readonly TelegramOptions _telegramOptions;

	public UserDeleteConfirmTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		AuditLogger audit)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_audit = audit;
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
		_telegramOptions = telegramOptions.Value;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserDeleteConfirm;

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
				SourceEvent: TelegramCallbackEvent.UserDeleteConfirm);

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
		var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

		var user = await repo.GetUserByTelegramIdForUpdateAsync(telegramId, cancellationToken);

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

		var described = AuditLogger.Describe(user);

		// До удаления, пока видны привязки: иначе ссылка-подписка удалённого продолжит отдавать его соединения
		await scope.ServiceProvider.GetRequiredService<SubscriptionService>().DetachAllAsync(telegramId, cancellationToken);

		repo.Remove(user);
		await uow.SaveChangesAsync(cancellationToken);

		await _audit.LogAsync(chatId, AuditAction.UserDeleted, described, cancellationToken);

		var back = BackTo(TelegramCallbackEvent.UserList, sourceEvent: TelegramCallbackEvent.AdminPanel);

		await _bot.SendMessage(
			chatId: chatId,
			text: "Пользователь удалён.",
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ Назад к списку", Encode(back))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}