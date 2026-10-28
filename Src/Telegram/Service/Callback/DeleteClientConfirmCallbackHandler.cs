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
/// Подтверждённое удаление клиента <see cref="TelegramCallbackEvent.DeleteClientConfirm"/>
/// </summary>
[UsedImplicitly]
public class DeleteClientConfirmCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IVpnApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramAccessService _access;
    private readonly AuditLogger _audit;
    private readonly ILogger<DeleteClientConfirmCallbackHandler> _logger;

    public DeleteClientConfirmCallbackHandler(
        ITelegramBotClient telegramBot,
        IVpnApiService xuiApiService,
        IServiceScopeFactory scopeFactory,
        TelegramAccessService access,
        AuditLogger audit,
        ILogger<DeleteClientConfirmCallbackHandler> logger)
        : base(telegramBot)
    {
        _audit = audit;
        _telegramBot = telegramBot;
        _xuiApiService = xuiApiService;
        _scopeFactory = scopeFactory;
        _access = access;
        _logger = logger;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.DeleteClientConfirm;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        if (!await _access.CanManagePeerAsync(chatId, request.Id, cancellationToken))
        {
            await DenyAsync(update, "Это соединение вам не принадлежит.", cancellationToken);
            return;
        }

        // Удаляем экран подтверждения, чтобы не плодить сообщения
        await TryDeleteOriginMessageAsync(update, cancellationToken);

        // 1) Удаляем на VPN-сервере
        try
        {
            await _xuiApiService.DeleteClientAsync(request.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Не удалось удалить клиента {ClientId} на VPN-сервере", request.Id);

            await _telegramBot.SendMessage(
                chatId: chatId,
                text: "Не удалось удалить клиента: сервер недоступен или вернул ошибку. Попробуйте позже.",
                cancellationToken: cancellationToken);

            await AnswerAsync(update, cancellationToken);
            return;
        }

        // 2) Удаляем в БД (если запись есть)
        string clientName;
        using (var scope = _scopeFactory.CreateScope())
        {
            var peerRepo = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var peer = await peerRepo.GetByClientIdAsync(request.Id, cancellationToken);
            clientName = peer?.Name ?? request.Id.ToString();
            if (peer is not null)
            {
                // Важно: связи VpnUsers удалятся каскадно по FK PeerId (у вас DeleteBehavior.Cascade)
                peerRepo.Remove(peer);
                await uow.SaveChangesAsync(cancellationToken);
            }
        }

        await _audit.LogAsync(chatId, AuditAction.ClientDeleted, $"'{clientName}'", cancellationToken);

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Клиент удалён.",
            cancellationToken: cancellationToken);

        // Возвращаем туда, откуда пришли (MyConnections или List)
        var backTarget = request.SourceEvent == TelegramCallbackEvent.MyConnections
            ? TelegramCallbackEvent.MyConnections
            : TelegramCallbackEvent.List;

        var backToList = new TelegramRequest(
            Id: Guid.Empty,
            CallbackEvent: backTarget,
            SourceEvent: backTarget,
            Page: request.Page);

        await _telegramBot.SendMessage(
            chatId: chatId,
            text: "Вернуться назад:",
            replyMarkup: new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backToList))),
            cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }
}