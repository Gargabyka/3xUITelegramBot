using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Админ панель
/// </summary>
[UsedImplicitly]
public sealed class AdminPanelTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;

	public AdminPanelTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.AdminPanel;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var chatId = update.CallbackQuery?.Message?.Chat.Id;
		if (chatId is null)
		{
			return;
		}

		if (!await IsAdminAsync(chatId.Value, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var listRequest = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.List,
			SourceEvent: TelegramCallbackEvent.MainMenu);

		var backRequest = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.MainMenu,
			SourceEvent: TelegramCallbackEvent.AdminPanel);

		var addUserRequest = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.AddUser,
			SourceEvent: TelegramCallbackEvent.AdminPanel);

		var listUsersRequest = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.UserList,
			SourceEvent: TelegramCallbackEvent.AdminPanel);

		var refreshConnectionsRequest = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.RefreshConnections,
			SourceEvent: TelegramCallbackEvent.AdminPanel);

		var markup = new InlineKeyboardMarkup(
		[
			[
				InlineKeyboardButton.WithCallbackData("📋 Список клиентов", TelegramCallbackDataCodec.Encode(listRequest)),
			],
			[
				InlineKeyboardButton.WithCallbackData("➕ Добавить пользователя", TelegramCallbackDataCodec.Encode(addUserRequest)),
			],
			[
				InlineKeyboardButton.WithCallbackData("👥 Список пользователей", TelegramCallbackDataCodec.Encode(listUsersRequest)),
			],
			[
				InlineKeyboardButton.WithCallbackData("🔄 Обновить соединения", TelegramCallbackDataCodec.Encode(refreshConnectionsRequest)),
			],
			[
				InlineKeyboardButton.WithCallbackData("⬅️ Назад", TelegramCallbackDataCodec.Encode(backRequest))
			],
		]);

		await _telegramBot.SendMessage(
			chatId: chatId.Value,
			text: "Админ панель:",
			replyMarkup: markup,
			cancellationToken: cancellationToken);
	}
}