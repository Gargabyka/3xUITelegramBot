using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store;

namespace Telegram.Service.Callback;

/// <summary>
/// Отмена ожидания ввода (переименование, рассылка) <see cref="TelegramCallbackEvent.CancelInput"/>
/// </summary>
[UsedImplicitly]
public sealed class CancelInputTelegramCallbackHandler : TelegramCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly ExpiringStateStore<RenameClientState> _renameStore;
	private readonly ExpiringStateStore<BroadcastState> _broadcastStore;

	public CancelInputTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		ExpiringStateStore<RenameClientState> renameStore,
		ExpiringStateStore<BroadcastState> broadcastStore)
		: base(telegramBot)
	{
		_telegramBot = telegramBot;
		_renameStore = renameStore;
		_broadcastStore = broadcastStore;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.CancelInput;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		_renameStore.Clear(chatId);
		_broadcastStore.Clear(chatId);

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: "Отменено.",
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ В главное меню", Encode(BackTo(TelegramCallbackEvent.MainMenu)))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
