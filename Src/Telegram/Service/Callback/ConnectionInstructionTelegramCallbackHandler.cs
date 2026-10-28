using Domain.Enums;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback;

/// <summary>
/// Показывает инструкцию по подключению для профиля соединения <see cref="TelegramCallbackEvent.ConnectionInstruction"/>
/// </summary>
[UsedImplicitly]
public class ConnectionInstructionTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;

    public ConnectionInstructionTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IServiceScopeFactory scopeFactory)
        : base(telegramBot)
    {
        _telegramBot = telegramBot;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.ConnectionInstruction;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        using var scope = _scopeFactory.CreateScope();
        var peerRepository = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
        var peer = await peerRepository.GetByClientIdAsync(request.Id, cancellationToken);

        // Для соединения без записи в БД показываем инструкцию по умолчанию (VLESS Raw)
        var profile = peer?.Profile ?? VpnProfileType.VlessRaw;

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        // Назад возвращает на карточку соединения
        var backRequest = request with { CallbackEvent = TelegramCallbackEvent.GetFullInfo };

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: ConnectionInstructions.Get(profile),
            replyMarkup: new InlineKeyboardMarkup([
                [InlineKeyboardButton.WithCallbackData("⬅ К соединению", Encode(backRequest))]
            ]),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}
