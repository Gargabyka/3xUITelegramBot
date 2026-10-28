using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store;

namespace Telegram.Service.Callback;

/// <summary>
/// Запрос нового имени соединения <see cref="TelegramCallbackEvent.RenameClient"/>
/// </summary>
[UsedImplicitly]
public sealed class RenameClientTelegramCallbackHandler : TelegramCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly TelegramAccessService _access;
	private readonly ExpiringStateStore<RenameClientState> _store;

	public RenameClientTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		TelegramAccessService access,
		ExpiringStateStore<RenameClientState> store)
		: base(telegramBot)
	{
		_telegramBot = telegramBot;
		_access = access;
		_store = store;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.RenameClient;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await _access.CanManagePeerAsync(chatId, request.Id, cancellationToken))
		{
			await DenyAsync(update, "Это соединение вам не принадлежит.", cancellationToken);
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		_store.Set(chatId, new RenameClientState(request.Id, request.SourceEvent, request.Page));

		var cancelRequest = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.CancelInput, TelegramCallbackEvent.MainMenu);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: $"""
			       Введите новое имя соединения одним сообщением.
			       {ClientNameRules.Hint}

			       Подключение продолжит работать — заново импортировать ссылку или конфиг не нужно.
			       """,
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("❌ Отмена", Encode(cancelRequest))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
