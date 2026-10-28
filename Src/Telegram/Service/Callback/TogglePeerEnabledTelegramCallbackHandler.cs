using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback;

/// <summary>
/// Включает/выключает клиента 3x-ui. Обычному пользователю доступно только для своих peer, админу — для любых.
/// </summary>
[UsedImplicitly]
public sealed class TogglePeerEnabledTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly ITelegramBotClient _bot;
    private readonly IVpnApiService _xuiApiService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramAccessService _access;
    private readonly AuditLogger _audit;
    private readonly ILogger<TogglePeerEnabledTelegramCallbackHandler> _logger;

    public TogglePeerEnabledTelegramCallbackHandler(
        ITelegramBotClient telegramBot,
        IVpnApiService xuiApiService,
        IServiceScopeFactory scopeFactory,
        TelegramAccessService access,
        AuditLogger audit,
        ILogger<TogglePeerEnabledTelegramCallbackHandler> logger)
        : base(telegramBot)
    {
        _audit = audit;
        _bot = telegramBot;
        _xuiApiService = xuiApiService;
        _scopeFactory = scopeFactory;
        _access = access;
        _logger = logger;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.TogglePeerEnabled;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        if (!await _access.CanManagePeerAsync(chatId, request.Id, cancellationToken))
        {
            await DenyAsync(update, "Это соединение вам не принадлежит.", cancellationToken);
            return;
        }

        await TryDeleteOriginMessageAsync(update, cancellationToken);

        var backRequest = new TelegramRequest(
            Id: request.Id,
            CallbackEvent: TelegramCallbackEvent.GetFullInfo,
            SourceEvent: request.SourceEvent,
            Page: request.Page);

        using var scope = _scopeFactory.CreateScope();

        // 1) Переключение через 3x-ui API — если тут упадёт, значит реально ничего не поменялось,
        // и пользователю можно честно сказать "не удалось".
        bool wasEnabled;
        string clientName;
        try
        {
            var clients = await _xuiApiService.GetClients(cancellationToken);
            var client = clients.FirstOrDefault(x => x.Id == request.Id);
            if (client is null)
            {
                await _bot.SendMessage(
                    chatId: chatId,
                    text: "Не удалось найти клиента.",
                    replyMarkup: new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backRequest))),
                    cancellationToken: cancellationToken);

                await AnswerAsync(update, cancellationToken);
                return;
            }

            wasEnabled = client.Enabled;
            clientName = client.Name;

            if (wasEnabled)
            {
                await _xuiApiService.DisableClientAsync(request.Id, cancellationToken);
            }
            else
            {
                await _xuiApiService.EnableClientAsync(request.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось переключить состояние peer {ClientId} через 3x-ui API", request.Id);

            await _bot.SendMessage(
                chatId: chatId,
                text: "Не удалось изменить состояние соединения. Попробуйте позже.",
                replyMarkup: new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backRequest))),
                cancellationToken: cancellationToken);

            await AnswerAsync(update, cancellationToken);
            return;
        }

        // 2) Локальная копия статуса — best-effort. 3x-ui API уже применил изменение,
        // поэтому сбой здесь не должен заставлять пользователя думать, что переключение не сработало
        // (иначе повторный тап отменит уже применённое изменение).
        try
        {
            var peerRepo = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var localPeer = await peerRepo.GetByClientIdAsync(request.Id, cancellationToken);
            if (localPeer is not null)
            {
                localPeer.IsEnabled = !wasEnabled;
                await uow.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Peer {ClientId} переключён в 3x-ui, но не удалось обновить локальную копию", request.Id);
        }

        await _audit.LogAsync(
            chatId,
            wasEnabled ? AuditAction.ClientDisabled : AuditAction.ClientEnabled,
            $"'{clientName}'",
            cancellationToken);

        var statusText = wasEnabled
            ? "Соединение отключено."
            : "Соединение включено.";

        await _bot.SendMessage(
            chatId: chatId,
            text: statusText,
            replyMarkup: new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backRequest))),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}