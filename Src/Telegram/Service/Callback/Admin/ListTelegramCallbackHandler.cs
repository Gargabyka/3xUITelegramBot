using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using XUi.Models;
using XUi.Services.Interface;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Обработчик события List <see cref="TelegramCallbackEvent.List"/>
/// </summary>
[UsedImplicitly]
public class ListTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private const int PageSize = 9;

    private readonly IXUiApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;

    public ListTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IXUiApiService xuiApiService)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _xuiApiService = xuiApiService;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.List;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        // Не захламляем чат: удаляем текущий экран (список/карточку/подтверждение)
        await TryDeleteOriginMessageAsync(update, cancellationToken);

        var clients = await _xuiApiService.GetClients(cancellationToken);
        if (clients is null or { Length: 0 })
        {
            var backToMenuEmpty = BackTo(TelegramCallbackEvent.AdminPanel, sourceEvent: TelegramCallbackEvent.AdminPanel);

            await _telegramBot.SendMessage(
                chatId: chatId,
                text: "Не удалось найти клиентов",
                replyMarkup: new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToMenuEmpty))),
                cancellationToken: cancellationToken);

            await AnswerAsync(update, cancellationToken);
            return;
        }

        var total = clients.Length;
        var totalPages = (int)Math.Ceiling(total / (double)PageSize);

        var page = request.Page;
        if (page < 0)
        {
            page = 0;
        }

        if (page > totalPages - 1)
        {
            page = totalPages - 1;
        }

        var pageClients = clients
            .OrderByDescending(c => c.CreatedAt)
            .Skip(page * PageSize)
            .Take(PageSize)
            .ToArray();

        var rows = BuildClientRows(pageClients);

        // Навигация
        var prevPage = Math.Max(0, page - 1);
        var nextPage = Math.Min(totalPages - 1, page + 1);

        var navPrev = new TelegramRequest(
            Id: Guid.Empty,
            CallbackEvent: TelegramCallbackEvent.List,
            SourceEvent: TelegramCallbackEvent.MainMenu,
            Page: prevPage);

        var navNext = new TelegramRequest(
            Id: Guid.Empty,
            CallbackEvent: TelegramCallbackEvent.List,
            SourceEvent: TelegramCallbackEvent.MainMenu,
            Page: nextPage);

        var canPrev = page > 0;
        var canNext = page < totalPages - 1;

        rows.Add([
            InlineKeyboardButton.WithCallbackData(canPrev ? "⬅️" : " ", Encode(navPrev)),
            InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", Encode(new TelegramRequest(Guid.Empty, TelegramCallbackEvent.List, TelegramCallbackEvent.AdminPanel, page))),
            InlineKeyboardButton.WithCallbackData(canNext ? "➡️" : " ", Encode(navNext)),
        ]);

        var backToMenu = BackTo(TelegramCallbackEvent.AdminPanel, sourceEvent: TelegramCallbackEvent.AdminPanel);
        rows.Add([
            InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToMenu))
        ]);

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Выберите клиента — покажу подробную информацию:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }

    private static List<InlineKeyboardButton[]> BuildClientRows(IEnumerable<XUiClient> clients)
    {
        var buttons = clients
            .Select(client => InlineKeyboardButton.WithCallbackData(
                text: client.Name,
                callbackData: TelegramCallbackDataCodec.Encode(
                    new TelegramRequest(
                        Id: client.Id,
                        CallbackEvent: TelegramCallbackEvent.GetFullInfo,
                        SourceEvent: TelegramCallbackEvent.List))))
            .ToList();

        var rows = new List<InlineKeyboardButton[]>();
        for (var i = 0; i < buttons.Count; i += 3)
        {
            rows.Add(buttons.Skip(i).Take(3).ToArray());
        }

        return rows;
    }
}