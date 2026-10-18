using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик отмены добавления клиента
/// </summary>
[UsedImplicitly]
public class AddClientCancelTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IAddClientStateStore _addClientState;
    private readonly ITelegramBotClient _telegramBot;

    public AddClientCancelTelegramCallbackHandler(ITelegramBotClient telegramBot, IAddClientStateStore addClientState)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _addClientState = addClientState;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.AddClientCancel;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        _addClientState.Clear(chatId);

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        var backToMenu = BackTo(TelegramCallbackEvent.MainMenu, sourceEvent: TelegramCallbackEvent.MainMenu);

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Добавление клиента отменено.",
            replyMarkup: new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("⬅ В главное меню", Encode(backToMenu))]
            ]),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}