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
public sealed class UserChangeConnectionLimitTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	// Пресеты лимита. -1 — специальное значение "без ограничений".
	private static readonly int[] Presets = [1, 2, 3, 5, 10, -1];

	private readonly ITelegramBotClient _bot;
	private readonly IServiceScopeFactory _scopeFactory;

	public UserChangeConnectionLimitTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserChangeConnectionLimit;

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
			SourceEvent: TelegramCallbackEvent.UserChangeConnectionLimit);

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

		// Page содержит выбранный пресет (см. Presets); 0 — ещё ничего не выбрано, показываем список
		if (Presets.Contains(request.Page))
		{
			user.MaxConnections = request.Page == -1 ? null : request.Page;
			await uow.SaveChangesAsync(cancellationToken);

			var statusText = user.MaxConnections.HasValue
				? $"Лимит соединений установлен: {user.MaxConnections.Value}."
				: "Лимит соединений снят (без ограничений).";

			await _bot.SendMessage(
				chatId: chatId,
				text: statusText,
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(backToUserInfo))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var current = user.MaxConnections;

		var presetButtons = Presets
			.Select(preset =>
			{
				var isSelected = preset == -1 ? current is null : current == preset;
				var label = preset == -1 ? "♾ Без лимита" : preset.ToString();

				return InlineKeyboardButton.WithCallbackData(
					text: (isSelected ? "✅ " : string.Empty) + label,
					callbackData: Encode(request with { Page = preset }));
			})
			.ToList();

		var rows = new List<InlineKeyboardButton[]>();
		for (var i = 0; i < presetButtons.Count; i += 3)
		{
			rows.Add(presetButtons.Skip(i).Take(3).ToArray());
		}

		rows.Add([InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToUserInfo))]);

		await _bot.SendMessage(
			chatId: chatId,
			text: $"Текущий лимит: {(current.HasValue ? current.Value.ToString() : "без ограничений")}.\nВыберите новый лимит соединений:",
			replyMarkup: new InlineKeyboardMarkup(rows),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
