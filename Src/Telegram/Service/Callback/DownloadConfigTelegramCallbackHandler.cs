using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Enums;
using Telegram.Models;
using XUi.Services.Interface;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик для скачивания конфигурации
/// </summary>
[UsedImplicitly]
public class DownloadConfigTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IXUiApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;

    public DownloadConfigTelegramCallbackHandler(
	    IXUiApiService xuiApiService,
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
        var document = await _xuiApiService.DownloadConfigAsync(request.Id, cancellationToken);
        await using var ms = new MemoryStream(document, writable: false);
        ms.Position = 0;

        var inputFile = InputFile.FromStream(ms, fileName: "vless.txt");
        var configName = await GetConfigName(request.Id, cancellationToken);

        await _telegramBot.SendDocument(
	        chatId: chatId,
	        document: inputFile,
	        caption: $"Ссылка VLESS для импорта в клиент {configName}",
	        cancellationToken: cancellationToken);

        await AnswerAsync(update, cancellationToken);
    }

    private async Task<string?> GetConfigName(Guid clientId, CancellationToken cancellationToken)
    {
	    var scope = _scopeFactory.CreateScope();
	    var vpnPeerRepository = scope.ServiceProvider.GetRequiredService<IVpnPeerRepository>();
	    var peer = await vpnPeerRepository.GetByClientIdAsync(clientId, cancellationToken);

	    return $"({peer?.Name})";
    }
}