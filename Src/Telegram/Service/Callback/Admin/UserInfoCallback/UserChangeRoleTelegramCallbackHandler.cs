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
public sealed class UserChangeRoleTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly TelegramOptions _telegramOptions;

	public UserChangeRoleTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
		_telegramOptions = telegramOptions.Value;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserChangeRole;

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
			SourceEvent: TelegramCallbackEvent.UserChangeRole);

		// Защита: пользователю из AdminIds нельзя менять роль "другим пользователям"
		if (_telegramOptions.AdminIds.Contains(telegramId) && chatId != telegramId)
		{
			await _bot.SendMessage(
				chatId: chatId,
				text: "Нельзя менять роль пользователю из списка AdminIds.",
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
				text: "Пользователь не найден.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backMissing))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		// Выбор роли делаем в один тап: если нажали на роль — назначаем и возвращаемся в карточку пользователя
		var setAdminRequest = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserChangeRole,
			SourceEvent: TelegramCallbackEvent.UserInfo,
			Page: (int)UserRole.Administrator);

		var setUserRequest = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserChangeRole,
			SourceEvent: TelegramCallbackEvent.UserInfo,
			Page: (int)UserRole.User);

		// Если Page содержит валидную роль — применяем
		if (request.Page is (int)UserRole.Administrator or (int)UserRole.User)
		{
			var newRole = (UserRole)request.Page;
			user.Role = newRole;
			await uow.SaveChangesAsync(cancellationToken);

			await _bot.SendMessage(
				chatId: chatId,
				text: $"Роль обновлена: {newRole}.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(backToUserInfo))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		// Иначе — показываем выбор
		var current = user.Role;

		var markup = new InlineKeyboardMarkup([
			[
				InlineKeyboardButton.WithCallbackData(
					text: (current == UserRole.Administrator ? "✅ " : string.Empty) + "Администратор",
					callbackData: Encode(setAdminRequest)),
			],
			[
				InlineKeyboardButton.WithCallbackData(
					text: (current == UserRole.User ? "✅ " : string.Empty) + "Пользователь",
					callbackData: Encode(setUserRequest)),
			],
			[
				InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToUserInfo)),
			]
		]);

		await _bot.SendMessage(
			chatId: chatId,
			text: "Выберите роль для пользователя:",
			replyMarkup: markup,
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}