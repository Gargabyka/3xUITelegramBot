using Domain.Extensions;
using Domain.Interfaces;
using Domain.Models;
using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Обработчик события List <see cref="TelegramCallbackEvent.List"/>
/// </summary>
[UsedImplicitly]
public class ListTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private const int PageSize = 9;

    private readonly IVpnApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;
    private readonly TelegramAccessService _access;

    public ListTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IVpnApiService xuiApiService,
        TelegramAccessService access)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _xuiApiService = xuiApiService;
        _access = access;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.List;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        // Список всех клиентов сервера — только для админов (кнопка могла остаться у бывшего админа)
        if (!await _access.IsAdminAsync(chatId, cancellationToken))
        {
            await DenyAsync(update, "Доступно только администраторам.", cancellationToken);
            return;
        }

        // Не захламляем чат: удаляем текущий экран (список/карточку/подтверждение)
        await TryDeleteOriginMessageAsync(update, cancellationToken);

        // Статусы, а не просто клиенты: недоступный сервер иначе молча пропадает из списка
        var statuses = await _xuiApiService.GetStatusAsync(cancellationToken);
        var clients = statuses
            .SelectMany(x => x.Clients)
            .GroupBy(x => x.Id)
            .Select(x => x.First()) // клиент, привязанный к нескольким inbound'ам, показывается один раз
            .ToArray();

        var unavailableNote = BuildUnavailableNote(statuses);

        if (clients.Length == 0)
        {
            var backToMenuEmpty = BackTo(TelegramCallbackEvent.AdminPanel, sourceEvent: TelegramCallbackEvent.AdminPanel);

            await _telegramBot.SendMessage(
                chatId: chatId,
                text: "Не удалось найти клиентов" + unavailableNote,
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
            text: $"""
                   Выберите клиента — покажу подробную информацию:

                   {string.Join("\n", pageClients.Select(x => ConnectionStatus.Line(x.Name, x.Profile, x)))}

                   {ConnectionStatus.Legend}
                   """ + unavailableNote,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }

    private static string BuildUnavailableNote(VpnBackendStatus[] statuses)
    {
        var unavailable = statuses
            .Where(x => !x.IsAvailable)
            .Select(x => $"• {x.Profile.Title}: {x.Error}")
            .ToArray();

        return unavailable.Length == 0
            ? string.Empty
            : $"\n\n⚠ Не ответили — их клиентов нет в списке:\n{string.Join("\n", unavailable)}";
    }

    private static List<InlineKeyboardButton[]> BuildClientRows(IEnumerable<VpnServerClient> clients)
    {
        // По одной кнопке в ряд, чтобы имя было видно целиком; статус — в тексте сообщения
        return clients
            .Select(client => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    text: ConnectionStatus.ButtonText(client.Name, client.Profile),
                    callbackData: TelegramCallbackDataCodec.Encode(
                        new TelegramRequest(
                            Id: client.Id,
                            CallbackEvent: TelegramCallbackEvent.GetFullInfo,
                            SourceEvent: TelegramCallbackEvent.List))),
            })
            .ToList();
    }
}