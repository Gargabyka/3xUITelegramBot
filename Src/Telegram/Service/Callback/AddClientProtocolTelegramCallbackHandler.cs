using Domain.Interfaces;
using JetBrains.Annotations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик выбора протокола при добавлении клиента <see cref="TelegramCallbackEvent.AddClientProtocol"/>
/// </summary>
[UsedImplicitly]
public class AddClientProtocolTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly IAddClientStateStore _addClientState;
    private readonly IVpnApiService _vpnApi;

    public AddClientProtocolTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IAddClientStateStore addClientState,
        IVpnApiService vpnApi)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _addClientState = addClientState;
        _vpnApi = vpnApi;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.AddClientProtocol;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        var profiles = _vpnApi.AvailableProfiles;
        if (request.Page < 0 || request.Page >= profiles.Count)
        {
            await AnswerAsync(update, cancellationToken);
            return;
        }

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        // Лимит соединений уже проверен на предыдущем шаге
        await AddClientPrompt.AskNameAsync(_telegramBot, _addClientState, chatId, profiles[request.Page], cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}
