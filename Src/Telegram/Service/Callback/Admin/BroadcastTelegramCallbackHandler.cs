using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Начало рассылки: запрос сообщения <see cref="TelegramCallbackEvent.Broadcast"/>
/// </summary>
[UsedImplicitly]
public sealed class BroadcastTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly ExpiringStateStore<BroadcastState> _store;

	public BroadcastTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		ExpiringStateStore<BroadcastState> store)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_store = store;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.Broadcast;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			await DenyAsync(update, "Доступно только администраторам.", cancellationToken);
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		_store.Set(chatId, new BroadcastState(MessageId: null));

		var cancelRequest = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.CancelInput, TelegramCallbackEvent.AdminPanel);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: """
			      📣 Рассылка

			      Отправьте сообщение, которое получат все пользователи бота: текст, фото, файл — как есть, с форматированием.
			      Перед отправкой я попрошу подтверждение.
			      """,
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("❌ Отмена", Encode(cancelRequest))),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}
}
