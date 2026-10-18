using System.Text;
using Domain.Entities;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using XUi.Models;
using XUi.Services.Interface;

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик события GetInfo
/// </summary>
[UsedImplicitly]
public class GetInfoTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IXUiApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;

    public GetInfoTelegramCallbackHandler(
        IXUiApiService xuiApiService,
        ITelegramBotClient telegramBot,
        IServiceScopeFactory scopeFactory)
        : base(telegramBot)
    {
        _xuiApiService = xuiApiService;
        _telegramBot = telegramBot;
        _scopeFactory = scopeFactory;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.GetFullInfo;

    public override async Task CallbackHandlerAsync(
	    Update update,
	    TelegramRequest request,
	    CancellationToken cancellationToken)
    {
	    var (chatId, _, _) = GetCallbackContext(update);

	    var clients = await _xuiApiService.GetClients(cancellationToken);
	    if (clients is null or { Length: 0 })
	    {
		    await _telegramBot.SendMessage(
			    chatId: chatId,
			    text: "Не удалось найти клиента",
			    cancellationToken: cancellationToken);
		    return;
	    }

	    var client = clients.SingleOrDefault(x => x.Id == request.Id);
	    if (client is null)
	    {
		    await _telegramBot.SendMessage(
			    chatId: chatId,
			    text: "Не удалось найти клиента",
			    cancellationToken: cancellationToken);
		    return;
	    }

	    await TryDeleteOriginMessageAsync(update, cancellationToken);

	    var configBytes = await _xuiApiService.DownloadConfigAsync(request.Id, cancellationToken);
	    var configText = Encoding.UTF8.GetString(configBytes)
		    .Replace("\r\n", "\n")
		    .Replace("\r", "\n");

	    var qrPng = BuildQrPng(configText);

	    var trafficSummary = await BuildTrafficSummaryAsync(client.Id, cancellationToken);

	    var caption = $"""
	                   {BuildClientInfoText(client)}

	                   {trafficSummary}

	                   Выберите действие:
	                   """;

	    // Назад возвращает туда, откуда пришли (MyConnections или List)
	    var backTarget = request.SourceEvent == TelegramCallbackEvent.MyConnections
		    ? TelegramCallbackEvent.MyConnections
		    : TelegramCallbackEvent.List;

	    var backRequest = new TelegramRequest(
		    Id: Guid.Empty,
		    CallbackEvent: backTarget,
		    SourceEvent: backTarget,
		    Page: request.Page);

	    var downloadRequest = WithEvent(
		    request,
		    TelegramCallbackEvent.DownloadConfig,
		    sourceEvent: TelegramCallbackEvent.GetFullInfo);

	    var deleteAskRequest = WithEvent(
		    request,
		    TelegramCallbackEvent.DeleteClientAskConfirm,
		    sourceEvent: request.SourceEvent);

	    var toggleRequest = WithEvent(
		    request,
		    TelegramCallbackEvent.TogglePeerEnabled,
		    sourceEvent: request.SourceEvent);

	    var markup = new InlineKeyboardMarkup([
		    [
			    InlineKeyboardButton.WithCallbackData("⬇ Скачать ссылку", Encode(downloadRequest))
		    ],
		    [
			    InlineKeyboardButton.WithCallbackData(
				    client.Enabled ? "⏸ Отключить" : "▶ Включить",
				    Encode(toggleRequest))
		    ],
		    [
			    InlineKeyboardButton.WithCallbackData("🗑 Удалить клиента", Encode(deleteAskRequest))
		    ],
		    [
			    InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backRequest))
		    ]
	    ]);

	    await using var ms = new MemoryStream(qrPng, writable: false);
	    ms.Position = 0;

	    var inputFile = InputFile.FromStream(ms, fileName: "vpn.png");

	    await _telegramBot.SendPhoto(
		    chatId: chatId,
		    photo: inputFile,
		    caption: caption,
		    parseMode: ParseMode.None,
		    replyMarkup: markup,
		    cancellationToken: cancellationToken);

	    await AnswerAsync(update, cancellationToken);
    }

    private static string BuildClientInfoText(XUiClient client)
    {
        var lastHandshake = client.LatestHandshakeAt.HasValue
            ? client.LatestHandshakeAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
            : "ещё не подключался";

        return $"""
                Имя клиента: {client.Name}
                Клиент включен: {(client.Enabled ? "да" : "нет")}
                Дата создания клиента: {client.CreatedAt:dd.MM.yyyy}
                Последняя активность: {lastHandshake}
                """;
    }

    private async Task<string> BuildTrafficSummaryAsync(Guid clientId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IVpnTrafficSnapshotRepository>();

        var firstSnapshotDate = await snapshotRepo.GetFirstSnapshotDateAsync(clientId, cancellationToken);
        if (firstSnapshotDate is null)
        {
            return "📊 Статистика трафика: пока нет данных (сбор начнётся в течение часа).";
        }

        var now = DateTime.UtcNow;

        // Запросы идут последовательно — DbContext не потокобезопасен для параллельных вызовов
        var day = await snapshotRepo.GetSnapshotsForPeriodAsync(clientId, now.AddDays(-1), cancellationToken);
        var week = await snapshotRepo.GetSnapshotsForPeriodAsync(clientId, now.AddDays(-7), cancellationToken);
        var month = await snapshotRepo.GetSnapshotsForPeriodAsync(clientId, now.AddDays(-30), cancellationToken);
        var all = await snapshotRepo.GetSnapshotsForPeriodAsync(clientId, null, cancellationToken);

        return $"""
                📊 Статистика трафика (учёт с {firstSnapshotDate.Value.ToLocalTime():dd.MM.yyyy HH:mm}):
                {FormatPeriodLine("За последние сутки", day)}
                {FormatPeriodLine("За последние 7 дней", week)}
                {FormatPeriodLine("За последние 30 дней", month)}
                {FormatPeriodLine("За всё время", all)}
                """;
    }

    private static string FormatPeriodLine(string label, VpnTrafficSnapshot[] snapshots)
    {
        var (rx, tx) = TrafficUsageCalculator.ComputeUsage(snapshots);
        return $"{label}: {TrafficFormatter.FormatBytes(rx + tx)}";
    }

    private static byte[] BuildQrPng(string payload)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule: 8);
    }
}