using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback;

/// <summary>
/// Показать подтверждение удаления клиента
/// </summary>
[UsedImplicitly]
public class DeleteClientAskConfirmCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly TelegramAccessService _access;

    public DeleteClientAskConfirmCallbackHandler(ITelegramBotClient telegramBot, TelegramAccessService access)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _access = access;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.DeleteClientAskConfirm;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        if (!await _access.CanManagePeerAsync(chatId, request.Id, cancellationToken))
        {
            await DenyAsync(update, "Это соединение вам не принадлежит.", cancellationToken);
            return;
        }

        // Удаляем текущий экран (QR), чтобы чат не захламлялся
        await TryDeleteOriginMessageAsync(update, cancellationToken);

        var confirmRequest = WithEvent(
            request,
            TelegramCallbackEvent.DeleteClientConfirm,
            sourceEvent: request.SourceEvent ?? TelegramCallbackEvent.GetFullInfo);

        var backToInfoRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: request.SourceEvent ?? TelegramCallbackEvent.GetFullInfo,
            SourceEvent: request.SourceEvent ?? TelegramCallbackEvent.GetFullInfo);

        var markup = new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData("✅ Да, удалить", Encode(confirmRequest))
            ],
            [
                InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToInfoRequest))
            ]
        ]);

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Точно удалить этого клиента? Действие необратимо.",
            replyMarkup: markup,
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}