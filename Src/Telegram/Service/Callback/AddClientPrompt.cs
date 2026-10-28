using Domain.Extensions;
using Domain.Models;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Service.Callback;

/// <summary>
/// Общий шаг сценария добавления клиента: запрос имени для выбранного протокола
/// </summary>
internal static class AddClientPrompt
{
    public static async Task AskNameAsync(
        ITelegramBotClient bot,
        IAddClientStateStore store,
        long chatId,
        VpnProfile profile,
        CancellationToken cancellationToken)
    {
        store.Begin(chatId, profile);

        var cancelRequest = new TelegramRequest(
            Id: Guid.Empty,
            CallbackEvent: TelegramCallbackEvent.AddClientCancel,
            SourceEvent: TelegramCallbackEvent.MainMenu,
            Page: 0);

        var markup = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("❌ Отмена", TelegramCallbackDataCodec.Encode(cancelRequest))
            ]
        ]);

        await bot.SendMessage(
            chatId: chatId,
            text: $"""
                   Профиль: {profile.Title} — {profile.Type.Description()}

                   Введите имя нового клиента одним сообщением.

                   Пример: iphone_ivan
                   """,
            replyMarkup: markup,
            cancellationToken: cancellationToken);
    }
}
