using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;
using Telegram.Store;

namespace Telegram.Service.TextFlows;

/// <summary>
/// Приём сообщения для рассылки и запрос подтверждения
/// </summary>
public sealed class BroadcastTextFlowHandler : ITelegramTextFlowHandler
{
	private readonly ITelegramBotClient _bot;
	private readonly ExpiringStateStore<BroadcastState> _store;
	private readonly TelegramAccessService _access;
	private readonly IServiceScopeFactory _scopeFactory;

	public BroadcastTextFlowHandler(
		ITelegramBotClient bot,
		ExpiringStateStore<BroadcastState> store,
		TelegramAccessService access,
		IServiceScopeFactory scopeFactory)
	{
		_bot = bot;
		_store = store;
		_access = access;
		_scopeFactory = scopeFactory;
	}

	public async Task<bool> TryHandleAsync(Update update, CancellationToken cancellationToken)
	{
		var message = update.Message;
		if (message is null || message.Text?.StartsWith('/') == true)
		{
			return false;
		}

		var chatId = message.Chat.Id;
		if (!_store.TryGet(chatId, out _))
		{
			return false;
		}

		if (!await _access.IsAdminAsync(chatId, cancellationToken))
		{
			_store.Clear(chatId);
			return false;
		}

		// Новое сообщение заменяет предыдущее, если админ передумал до подтверждения
		_store.Set(chatId, new BroadcastState(message.MessageId));

		int recipients;
		using (var scope = _scopeFactory.CreateScope())
		{
			var users = await scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>().GetUsersAsync(cancellationToken);
			recipients = users.Count(x => x.TelegramId != chatId);
		}

		var sendRequest = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.BroadcastSend, TelegramCallbackEvent.AdminPanel);
		var cancelRequest = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.CancelInput, TelegramCallbackEvent.AdminPanel);

		await _bot.SendMessage(
			chatId: chatId,
			text: $"Отправить это сообщение получателям: {recipients}? Можно прислать другое сообщение вместо него.",
			replyParameters: message.MessageId,
			replyMarkup: new InlineKeyboardMarkup([
				[InlineKeyboardButton.WithCallbackData($"✅ Отправить ({recipients})", TelegramCallbackDataCodec.Encode(sendRequest))],
				[InlineKeyboardButton.WithCallbackData("❌ Отмена", TelegramCallbackDataCodec.Encode(cancelRequest))],
			]),
			cancellationToken: cancellationToken);

		return true;
	}
}
