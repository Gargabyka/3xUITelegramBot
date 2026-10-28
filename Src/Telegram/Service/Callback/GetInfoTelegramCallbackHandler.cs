using System.Text;
using Domain.Entities;
using Domain.Enums;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;
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

namespace Telegram.Service.Callback;

/// <summary>
/// Обработчик события GetInfo
/// </summary>
[UsedImplicitly]
public class GetInfoTelegramCallbackHandler : TelegramCallbackHandlerBase
{
    private readonly IVpnApiService _xuiApiService;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramAccessService _access;

    public GetInfoTelegramCallbackHandler(
        IVpnApiService xuiApiService,
        ITelegramBotClient telegramBot,
        IServiceScopeFactory scopeFactory,
        TelegramAccessService access)
        : base(telegramBot)
    {
        _xuiApiService = xuiApiService;
        _telegramBot = telegramBot;
        _scopeFactory = scopeFactory;
        _access = access;
    }

    public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.GetFullInfo;

    public override async Task CallbackHandlerAsync(
	    Update update,
	    TelegramRequest request,
	    CancellationToken cancellationToken)
    {
	    var (chatId, _, _) = GetCallbackContext(update);

	    if (!await _access.CanManagePeerAsync(chatId, request.Id, cancellationToken))
	    {
		    await DenyAsync(update, "Это соединение вам не принадлежит.", cancellationToken);
		    return;
	    }

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

	    var qrPng = TryBuildQrPng(configText);

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

	    var instructionRequest = WithEvent(
		    request,
		    TelegramCallbackEvent.ConnectionInstruction,
		    sourceEvent: request.SourceEvent);

	    var renameRequest = WithEvent(
		    request,
		    TelegramCallbackEvent.RenameClient,
		    sourceEvent: request.SourceEvent);

	    var markup = new InlineKeyboardMarkup([
		    [
			    InlineKeyboardButton.WithCallbackData(client.Profile == VpnProfileType.AmneziaWg ? "⬇ Скачать конфиг" : "⬇ Скачать ссылку", Encode(downloadRequest))
		    ],
		    [
			    InlineKeyboardButton.WithCallbackData("📖 Инструкция", Encode(instructionRequest))
		    ],
		    [
			    InlineKeyboardButton.WithCallbackData("✏ Переименовать", Encode(renameRequest))
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

	    if (qrPng is null)
	    {
		    // Конфигурация слишком большая для QR-кода — отправляем карточку без картинки
		    await _telegramBot.SendMessage(
			    chatId: chatId,
			    text: caption + "\n\nQR-код недоступен: используйте скачивание конфига.",
			    replyMarkup: markup,
			    cancellationToken: cancellationToken);
	    }
	    else
	    {
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
	    }

	    await AnswerAsync(update, cancellationToken);
    }

    private static string BuildClientInfoText(VpnServerClient client)
    {
        var lastHandshake = client.LatestHandshakeAt.HasValue
            ? client.LatestHandshakeAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
            : "ещё не подключался";

        return $"""
                Профиль: {client.Profile.Title()}
                Имя клиента: {client.Name}
                Клиент включен: {(client.Enabled ? "да" : "нет")}
                Дата создания клиента: {client.CreatedAt:dd.MM.yyyy}
                Последняя активность: {lastHandshake}
                """;
    }

    private static string FormatPeriodLine(string label, VpnTrafficSnapshot[] snapshots)
    {
	    var (rx, tx) = TrafficUsageCalculator.ComputeUsage(snapshots);
	    return $"{label}: {TrafficFormatter.FormatBytes(rx + tx)}";
    }

    private static byte[]? TryBuildQrPng(string payload)
    {
	    try
	    {
		    using var gen = new QRCodeGenerator();
		    using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
		    using var qr = new PngByteQRCode(data);
		    return qr.GetGraphic(pixelsPerModule: 8);
	    }
	    catch (QRCoder.Exceptions.DataTooLongException)
	    {
		    return null;
	    }
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
}