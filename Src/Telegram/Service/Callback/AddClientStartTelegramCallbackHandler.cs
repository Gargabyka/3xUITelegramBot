using Domain.Extensions;
using Domain.Interfaces;
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
    private readonly IVpnApiService _vpnApi;

    public AddClientStartTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IAddClientStateStore addClientState,
        IServiceScopeFactory scopeFactory,
        IVpnApiService vpnApi)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _addClientState = addClientState;
        _scopeFactory = scopeFactory;
        _vpnApi = vpnApi;
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

        var profiles = _vpnApi.AvailableProfiles;
        if (profiles.Count == 0)
        {
            await _telegramBot.SendMessage(
                chatId: chatId,
                text: "Нет доступных вариантов подключения. Обратитесь к администратору.",
                cancellationToken: cancellationToken);

            await AnswerAsync(update, cancellationToken);
            return;
        }

        // Единственный сервер — выбирать нечего
        if (profiles.Count == 1)
        {
            await AddClientPrompt.AskNameAsync(_telegramBot, _addClientState, chatId, profiles[0], cancellationToken);
            await AnswerAsync(update, cancellationToken);
            return;
        }

        var rows = profiles
            .Select((profile, index) => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    profile.Title,
                    Encode(new TelegramRequest(
                        Id: Guid.Empty,
                        CallbackEvent: TelegramCallbackEvent.AddClientProtocol,
                        SourceEvent: TelegramCallbackEvent.MainMenu,
                        Page: index))),
            })
            .Append([InlineKeyboardButton.WithCallbackData(
                "❌ Отмена",
                Encode(new TelegramRequest(Guid.Empty, TelegramCallbackEvent.AddClientCancel, TelegramCallbackEvent.MainMenu)))])
            .ToArray();

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Выберите, куда создать соединение:\n\n"
                  + string.Join("\n\n", profiles.Select(p => $"• {p.Title} — {p.Type.Description()}")),
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}