using Domain.Entities;
using Domain.Enums;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик для скачивания конфигурации
/// </summary>
[UsedImplicitly]
public class DownloadConfigTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IVpnApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;

    public DownloadConfigTelegramCallbackHandler(
	    IVpnApiService xuiApiService,
	    ITelegramBotClient telegramBot,
	    IServiceScopeFactory scopeFactory)
        : base(telegramBot)
    {
        _xuiApiService = xuiApiService;
        _telegramBot = telegramBot;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.DownloadConfig;

    public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
    {
        var (chatId, _, _) = GetCallbackContext(update);

        // Тут специально НЕ удаляем исходное сообщение (QR-экран) — пусть остаётся,
        // а конфиг просто прилетит отдельным документом.
        var peer = await GetPeerAsync(request.Id, cancellationToken);
        var profile = peer?.Profile ?? VpnProfileType.VlessRaw;

        var document = await _xuiApiService.DownloadConfigAsync(request.Id, cancellationToken);
        await using var ms = new MemoryStream(document, writable: false);
        ms.Position = 0;

        var inputFile = InputFile.FromStream(ms, fileName: profile.ConfigFileName());

        await _telegramBot.SendDocument(
	        chatId: chatId,
	        document: inputFile,
	        caption: $"{profile.ConfigKind()} для импорта в клиент ({peer?.Name})",
	        cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }

    private async Task<VpnPeer?> GetPeerAsync(Guid clientId, CancellationToken cancellationToken)
    {
	    using var scope = _scopeFactory.CreateScope();
	    var vpnPeerRepository = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();

	    return await vpnPeerRepository.GetByClientIdAsync(clientId, cancellationToken);
    }
}