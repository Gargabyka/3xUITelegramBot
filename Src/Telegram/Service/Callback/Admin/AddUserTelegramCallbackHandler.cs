using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Service.Callback.Admin;

[UsedImplicitly]
public sealed class AddUserTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;

    public AddUserTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IOptions<TelegramOptions> telegramOptions,
        IServiceScopeFactory scopeFactory)
        : base(telegramBot, telegramOptions, scopeFactory)
    {
        _bot = telegramBot;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.AddUser;

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

        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetService<IAddUserStateStore>();
        if (store is null)
        {
            await _bot.SendMessage(chatId.Value, "Ошибка: IAddUserStateStore не зарегистрирован.", cancellationToken: cancellationToken);
            return;
        }

        store.Begin(chatId.Value);

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { KeyboardButton.WithRequestContact("📇 Отправить контакт") },
            new[] { new KeyboardButton("Отмена") },
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
        };

        await _bot.SendMessage(
            chatId: chatId.Value,
            text: "Отправьте контакт пользователя кнопкой ниже. Или нажмите «Отмена».",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}