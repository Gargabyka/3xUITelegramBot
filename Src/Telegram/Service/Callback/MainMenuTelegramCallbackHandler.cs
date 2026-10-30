using Domain.Enums;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик для главного меню <see cref="TelegramCallbackEvent.MainMenu"/>
/// </summary>
[UsedImplicitly]
public class MainMenuTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly TelegramOptions _telegramOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    public MainMenuTelegramCallbackHandler(
	    ITelegramBotClient telegramBot,
	    IOptions<TelegramOptions> telegramOptions,
	    IServiceScopeFactory scopeFactory)
        : base(telegramBot)
    {
	    _telegramBot = telegramBot;
	    _telegramOptions = telegramOptions.Value;
	    _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.MainMenu;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
	    var (chatId, _, _) = GetCallbackContext(update);

	    await TryDeleteOriginMessageAsync(update, cancellationToken);

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
			    InlineKeyboardButton.WithCallbackData("🔌 Список соединений", Encode(myConnectionsRequest)),
		    },
		    new()
		    {
			    InlineKeyboardButton.WithCallbackData("➕ Добавить соединение", Encode(addRequest)),
		    },
	    };

	    if (await IsAdminAsync(chatId, cancellationToken))
        {
	        var adminPanelRequest = new TelegramRequest(
		        Id: Guid.Empty,
		        CallbackEvent: TelegramCallbackEvent.AdminPanel,
		        SourceEvent: TelegramCallbackEvent.MainMenu);

	        buttons.Add(
	        [
		        InlineKeyboardButton.WithCallbackData(
			        "🛠 Админ панель",
			        Encode(adminPanelRequest))
	        ]);
        }

	    await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Главное меню:",
            replyMarkup: buttons,
            cancellationToken: cancellationToken);

	    await AnswerAsync(update, cancellationToken);
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