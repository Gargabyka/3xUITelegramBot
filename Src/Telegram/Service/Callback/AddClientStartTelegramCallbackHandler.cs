using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик события AddClientStart <see cref="TelegramCallbackEvent.AddClient"/>
/// </summary>
[UsedImplicitly]
public class AddClientStartTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IAddClientStateStore _addClientState;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;

    public AddClientStartTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IAddClientStateStore addClientState,
        IServiceScopeFactory scopeFactory)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _addClientState = addClientState;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.AddClient;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        using (var scope = _scopeFactory.CreateScope())
        {
            var telegramUserRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
            var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();

            var user = await telegramUserRepo.GetUserByTelegramIdAsync(chatId, cancellationToken);
            if (user?.MaxConnections is { } limit)
            {
                var currentCount = (await vpnUserRepo.GetConnectionsByTelegramIdAsync(user.Id, cancellationToken)).Length;
                if (currentCount >= limit)
                {
                    await _telegramBot.SendMessage(
                        chatId: chatId,
                        text: $"Достигнут лимит соединений: {currentCount}/{limit}. Обратитесь к администратору, чтобы увеличить лимит.",
                        cancellationToken: cancellationToken);

                    await AnswerAsync(update, cancellationToken);
                    return;
                }
            }
        }

        _addClientState.Begin(chatId);

        var cancelRequest = new TelegramRequest(
            Id: Guid.Empty,
            CallbackEvent: TelegramCallbackEvent.AddClientCancel,
            SourceEvent: TelegramCallbackEvent.MainMenu,
            Page: 0);

        var markup = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("❌ Отмена", Encode(cancelRequest))
            ]
        ]);

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: """
                  Введите имя нового клиента одним сообщением.

                  Пример: iphone_ivan
                  """,
            replyMarkup: markup,
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}