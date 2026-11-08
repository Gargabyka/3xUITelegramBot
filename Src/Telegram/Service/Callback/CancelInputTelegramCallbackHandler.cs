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
/// Отмена ожидания ввода (переименование, рассылка) <see cref="TelegramCallbackEvent.CancelInput"/>
/// </summary>
[UsedImplicitly]
public sealed class CancelInputTelegramCallbackHandler : TelegramCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly ExpiringStateStore<RenameClientState> _renameStore;
	private readonly ExpiringStateStore<BroadcastState> _broadcastStore;
	private readonly ExpiringStateStore<HostingReminderInputState> _hostingReminderInputs;
	private readonly HostingReminderService _hostingReminders;

	public CancelInputTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		ExpiringStateStore<RenameClientState> renameStore,
		ExpiringStateStore<BroadcastState> broadcastStore,
		ExpiringStateStore<HostingReminderInputState> hostingReminderInputs,
		HostingReminderService hostingReminders)
		: base(telegramBot)
	{
		_telegramBot = telegramBot;
		_renameStore = renameStore;
		_broadcastStore = broadcastStore;
		_hostingReminderInputs = hostingReminderInputs;
		_hostingReminders = hostingReminders;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.CancelInput;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		var wasHostingReminderInput = _hostingReminderInputs.TryGet(chatId, out _);

		_renameStore.Clear(chatId);
		_broadcastStore.Clear(chatId);
		_hostingReminderInputs.Clear(chatId);

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		// Отмена ввода даты или текста напоминания — обратно на экран «Оплата сервера»
		if (wasHostingReminderInput)
		{
			await HostingReminderScreen.SendAsync(_telegramBot, _hostingReminders, chatId, "Отменено.", cancellationToken);
			await AnswerAsync(update, cancellationToken);
			return;
		}

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: "Отменено.",
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ В главное меню", Encode(BackTo(TelegramCallbackEvent.MainMenu)))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
