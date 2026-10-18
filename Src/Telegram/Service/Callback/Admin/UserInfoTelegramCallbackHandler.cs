using Domain.Extensions;
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
public sealed class UserInfoTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;

    public UserInfoTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IOptions<TelegramOptions> telegramOptions,
        IServiceScopeFactory scopeFactory)
        : base(telegramBot, telegramOptions, scopeFactory)
    {
        _bot = telegramBot;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserInfo;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        var telegramId = LongGuidCodec.Decode(request.Id);

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

        var user = await repo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
        if (user is null)
        {
            var backMissing = BackTo(TelegramCallbackEvent.UserList, sourceEvent: TelegramCallbackEvent.AdminPanel);

            await _bot.SendMessage(
                chatId: chatId,
                text: "Пользователь не найден.",
                replyMarkup: new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithCallbackData("⬅ Назад к списку", Encode(backMissing))),
                cancellationToken: cancellationToken);

            await AnswerAsync(update, cancellationToken);
            return;
        }

        var text = $"""
                   Пользователь:

                   Имя: {user.FirstName ?? "-"}
                   Фамилия: {user.LastName ?? "-"}
                   Username: {(string.IsNullOrWhiteSpace(user.UserName) ? "-" : "@" + user.UserName.TrimStart('@'))}
                   TelegramId: {user.TelegramId}
                   Роль: {user.Role.GetDescription()}
                   Добавлен: {user.CreatedAt:yyyy-MM-dd}
                   Напоминания об оплате: {(user.ReminderOptOut ? "🔕 отключены" : "🔔 включены")}
                   Лимит соединений: {(user.MaxConnections.HasValue ? user.MaxConnections.Value.ToString() : "без ограничений")}
                   """;

        var connectionsRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.UserConnections,
            SourceEvent: TelegramCallbackEvent.UserInfo);

        var addConnectionRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.UserAddConnection,
            SourceEvent: TelegramCallbackEvent.UserInfo);

        var changeRoleRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.UserChangeRole,
            SourceEvent: TelegramCallbackEvent.UserInfo);

        var deleteUserRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.UserDeleteAskConfirm,
            SourceEvent: TelegramCallbackEvent.UserInfo);

        var toggleReminderRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.UserToggleReminderOptOut,
            SourceEvent: TelegramCallbackEvent.UserInfo);

        var changeLimitRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.UserChangeConnectionLimit,
            SourceEvent: TelegramCallbackEvent.UserInfo);

        var back = BackTo(TelegramCallbackEvent.UserList, sourceEvent: TelegramCallbackEvent.AdminPanel);

        var markup = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("🔌 Посмотреть соединения", Encode(connectionsRequest)),
            ],
            [
                InlineKeyboardButton.WithCallbackData("➕ Добавить соединение", Encode(addConnectionRequest)),
            ],
            [
                InlineKeyboardButton.WithCallbackData("👤 Изменить роль", Encode(changeRoleRequest)),
            ],
            [
                InlineKeyboardButton.WithCallbackData("🔢 Изменить лимит соединений", Encode(changeLimitRequest)),
            ],
            [
                InlineKeyboardButton.WithCallbackData(
                    user.ReminderOptOut ? "🔔 Включить напоминания" : "🔕 Отключить напоминания",
                    Encode(toggleReminderRequest)),
            ],
            [
                InlineKeyboardButton.WithCallbackData("🗑 Удалить пользователя", Encode(deleteUserRequest)),
            ],
            [
                InlineKeyboardButton.WithCallbackData("⬅ Назад к списку", Encode(back))
            ]
        ]);

        await _bot.SendMessage(
            chatId: chatId,
            text: text,
            replyMarkup: markup,
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}