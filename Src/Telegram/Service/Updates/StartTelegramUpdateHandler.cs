using Domain.Enums;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;

namespace Telegram.Service.Updates;

/// <summary>
/// Обработчик обновления команды /start
/// </summary>
public class StartTelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly TelegramOptions _telegramOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    public StartTelegramUpdateHandler(
	    ITelegramBotClient telegramBot,
	    IOptions<TelegramOptions> telegramOptions,
	    IServiceScopeFactory scopeFactory)
    {
	    _telegramBot = telegramBot;
	    _scopeFactory = scopeFactory;
	    _telegramOptions = telegramOptions.Value;
    }

    public async Task UpdateHandlerAsync(Update update, CancellationToken cancellationToken)
    {
	    var chatId = update.Message?.Chat.Id;
	    if (chatId is null)
	    {
		    return;
	    }

	    var myConnectionsRequest = new TelegramRequest(
		    Id: Guid.Empty,
		    CallbackEvent: TelegramCallbackEvent.MyConnections,
		    SourceEvent: TelegramCallbackEvent.MainMenu);

	    var addRequest = new TelegramRequest(
		    Id: Guid.Empty,
		    CallbackEvent: TelegramCallbackEvent.AddClient,
		    SourceEvent: TelegramCallbackEvent.MainMenu);

	    var buttons = new List<List<InlineKeyboardButton>>
	    {
		    new()
		    {
			    InlineKeyboardButton.WithCallbackData("🔌 Список соединений", TelegramCallbackDataCodec.Encode(myConnectionsRequest)),
		    },
		    new()
		    {
			    InlineKeyboardButton.WithCallbackData("➕ Добавить клиента", TelegramCallbackDataCodec.Encode(addRequest)),
		    },
	    };

	    if (await IsAdminAsync(chatId.Value, cancellationToken))
	    {
		    var adminPanelRequest = new TelegramRequest(
			    Id: Guid.Empty,
			    CallbackEvent: TelegramCallbackEvent.AdminPanel,
			    SourceEvent: TelegramCallbackEvent.MainMenu);

		    buttons.Add(
		    [
			    InlineKeyboardButton.WithCallbackData(
				    "🛠 Админ панель",
				    TelegramCallbackDataCodec.Encode(adminPanelRequest))
		    ]);
	    }

	    await _telegramBot.SendMessage(
		    chatId: chatId.Value,
		    text: "Главное меню:",
		    replyMarkup: buttons,
		    cancellationToken: cancellationToken);
    }

    private async Task<bool> IsAdminAsync(long telegramId, CancellationToken cancellationToken)
    {
	    if (_telegramOptions.AdminIds.Contains(telegramId))
	    {
		    return true;
	    }

	    var scope = _scopeFactory.CreateScope();
	    var repo = scope.ServiceProvider.GetService<ITelegramUserRepository>();
	    if (repo is null)
	    {
		    return false;
	    }

	    var user = await repo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
	    return user?.Role == UserRole.Administrator;
    }
}