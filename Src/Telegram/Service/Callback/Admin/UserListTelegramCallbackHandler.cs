using Domain.Interfaces.Repository;
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

[UsedImplicitly]
public sealed class UserListTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
    private const int PageSize = 9;

    private readonly ITelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;

    public UserListTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IOptions<TelegramOptions> telegramOptions,
        IServiceScopeFactory scopeFactory)
        : base(telegramBot, telegramOptions, scopeFactory)
    {
        _bot = telegramBot;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserList;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

        var users = await repo.GetUsersAsync(cancellationToken);
        if (users.Length == 0)
        {
            var back = BackTo(TelegramCallbackEvent.AdminPanel, sourceEvent: TelegramCallbackEvent.AdminPanel);

            await _bot.SendMessage(
                chatId: chatId,
                text: "Пользователей пока нет.",
                replyMarkup: new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(back))),
                cancellationToken: cancellationToken);

            await AnswerAsync(update, cancellationToken);
            return;
        }

        var ordered = users
            .OrderBy(u => u.FirstName ?? string.Empty)
            .ThenBy(u => u.LastName ?? string.Empty)
            .ThenBy(u => u.UserName ?? string.Empty)
            .ToArray();

        var totalPages = (int)Math.Ceiling(ordered.Length / (double)PageSize);

        var page = request.Page;
        if (page < 0)
		{
			page = 0;
		}

        if (page > totalPages - 1)
		{
			page = totalPages - 1;
		}

        var pageUsers = ordered
            .Skip(page * PageSize)
            .Take(PageSize)
            .ToArray();

        var rows = BuildUserRows(pageUsers);

        // Навигация
        var prevPage = Math.Max(0, page - 1);
        var nextPage = Math.Min(totalPages - 1, page + 1);

        var navPrev = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserList, TelegramCallbackEvent.AdminPanel, prevPage);
        var navNext = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserList, TelegramCallbackEvent.AdminPanel, nextPage);

        var canPrev = page > 0;
        var canNext = page < totalPages - 1;

        rows.Add([
            InlineKeyboardButton.WithCallbackData(canPrev ? "⬅️" : " ", Encode(navPrev)),
            InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", Encode(new TelegramRequest(Guid.Empty, TelegramCallbackEvent.UserList, TelegramCallbackEvent.AdminPanel, page))),
            InlineKeyboardButton.WithCallbackData(canNext ? "➡️" : " ", Encode(navNext)),
        ]);

        var backToAdmin = BackTo(TelegramCallbackEvent.AdminPanel, sourceEvent: TelegramCallbackEvent.AdminPanel);
        rows.Add([InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToAdmin))]);

        await _bot.SendMessage(
            chatId: chatId,
            text: "Пользователи (нажмите на имя для подробностей):",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }

    private static List<InlineKeyboardButton[]> BuildUserRows(IEnumerable<Domain.Entities.TelegramUser> users)
    {
        static string DisplayName(Domain.Entities.TelegramUser u)
        {
            var full = $"{u.FirstName} {u.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(full))
			{
				return full;
			}

            if (!string.IsNullOrWhiteSpace(u.UserName))
			{
				return "@" + u.UserName.TrimStart('@');
			}

            return $"TelegramId: {u.TelegramId}";
        }

        var buttons = users
            .Select(u =>
                InlineKeyboardButton.WithCallbackData(
                    text: DisplayName(u),
                    callbackData: TelegramCallbackDataCodec.Encode(
                        new TelegramRequest(
                            Id: LongGuidCodec.Encode(u.TelegramId),
                            CallbackEvent: TelegramCallbackEvent.UserInfo,
                            SourceEvent: TelegramCallbackEvent.UserList))))
            .ToList();

        var rows = new List<InlineKeyboardButton[]>();
        for (var i = 0; i < buttons.Count; i += 2)
		{
			rows.Add(buttons.Skip(i).Take(2).ToArray());
		}

        return rows;
    }
}