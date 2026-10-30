using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;
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
/// Мои соединения (соединения текущего пользователя по TelegramId=chatId)
/// </summary>
[UsedImplicitly]
public sealed class MyConnectionsTelegramCallbackHandler : TelegramCallbackHandlerBase
{
	private const int PageSize = 9;

	private readonly ITelegramBotClient _bot;
	private readonly IVpnApiService _xuiApiService;
	private readonly IServiceScopeFactory _scopeFactory;

	public MyConnectionsTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IVpnApiService xuiApiService,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot)
	{
		_bot = telegramBot;
		_xuiApiService = xuiApiService;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.MyConnections;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		using var scope = _scopeFactory.CreateScope();
		var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();
		var telegramUserRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

		var user = await telegramUserRepo.GetUserByTelegramIdAsync(chatId, cancellationToken);
		if (user is null)
		{
			await _bot.SendMessage(
				chatId: chatId,
				text: $"Не удалось найти пользователя: {chatId}",
				cancellationToken: cancellationToken);

			return;
		}

		var connections = await vpnUserRepo.GetConnectionsByTelegramIdAsync(user.Id, cancellationToken);

		if (connections.Length == 0)
		{
			var backEmpty = BackTo(TelegramCallbackEvent.MainMenu, sourceEvent: TelegramCallbackEvent.MyConnections);

			await _bot.SendMessage(
				chatId: chatId,
				text: "У вас пока нет соединений.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(backEmpty))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var ordered = connections
			.OrderByDescending(x => x.Peer.CreatedAt)
			.ToArray();

		// Трафик берём live из 3x-ui API — если он недоступен, просто не показываем статистику
		Dictionary<Guid, VpnServerClient> clientsById = [];
		try
		{
			var clients = await _xuiApiService.GetClients(cancellationToken);
			clientsById = clients.ToDictionary(x => x.Id);
		}
		catch (Exception)
		{
			// покажем список без статистики трафика
		}

		var totalPages = (int)Math.Ceiling(ordered.Length / (double)PageSize);

		var page = request.Page;
		if (page < 0)
		{
			page = 0;
		}

		if (page > totalPages - 1)
		{
			page = totalPages - 1;
		}

		var pageConnections = ordered
			.Skip(page * PageSize)
			.Take(PageSize)
			.ToArray();

		var rows = BuildPeerRows(pageConnections, page, clientsById);

		// Общий трафик считаем по снимкам (переживают рестарт xray или сброса трафика),
		// а не по живым счётчикам API — иначе после рестарта тут показывался бы 0 вместо реальной суммы.
		var snapshotRepo = scope.ServiceProvider.GetRequiredService<IVpnTrafficSnapshotRepository>();
		long totalBytes = 0;
		foreach (var connection in ordered)
		{
			var snapshots = await snapshotRepo.GetSnapshotsForPeriodAsync(connection.Peer.ClientId, sinceUtc: null, cancellationToken);
			var (rx, tx) = TrafficUsageCalculator.ComputeUsage(snapshots);
			totalBytes += rx + tx;
		}

		// Навигация
		var prevPage = Math.Max(0, page - 1);
		var nextPage = Math.Min(totalPages - 1, page + 1);

		var navPrev = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.MyConnections,
			SourceEvent: TelegramCallbackEvent.MainMenu,
			Page: prevPage);

		var navNext = new TelegramRequest(
			Id: Guid.Empty,
			CallbackEvent: TelegramCallbackEvent.MyConnections,
			SourceEvent: TelegramCallbackEvent.MainMenu,
			Page: nextPage);

		var canPrev = page > 0;
		var canNext = page < totalPages - 1;

		rows.Add([
			InlineKeyboardButton.WithCallbackData(canPrev ? "⬅️" : " ", Encode(navPrev)),
			InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", Encode(new TelegramRequest(Guid.Empty, TelegramCallbackEvent.MyConnections, TelegramCallbackEvent.MainMenu, page))),
			InlineKeyboardButton.WithCallbackData(canNext ? "➡️" : " ", Encode(navNext)),
		]);

		var back = BackTo(TelegramCallbackEvent.MainMenu, sourceEvent: TelegramCallbackEvent.MyConnections);
		rows.Add([InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(back))]);

		var trafficLine = $"Всего использовано трафика: {TrafficFormatter.FormatBytes(totalBytes)}";

		await _bot.SendMessage(
			chatId: chatId,
			text: $"""
			       Ваши соединения (нажмите на имя для подробностей):
			       {ConnectionStatus.Legend}
			       {trafficLine}
			       """,
			replyMarkup: new InlineKeyboardMarkup(rows),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}

	private static List<InlineKeyboardButton[]> BuildPeerRows(
		IEnumerable<Domain.Entities.VpnUser> connections,
		int page,
		IReadOnlyDictionary<Guid, VpnServerClient> clientsById)
	{
		return ConnectionStatus.ToGrid(connections.Select(c =>
			InlineKeyboardButton.WithCallbackData(
				text: ConnectionStatus.ButtonText(c.Peer.Name, c.Peer.Profile, clientsById.GetValueOrDefault(c.Peer.ClientId)),
				callbackData: TelegramCallbackDataCodec.Encode(
					new TelegramRequest(
						Id: c.Peer.ClientId, // Guid клиента 3x-ui
						CallbackEvent: TelegramCallbackEvent.GetFullInfo,
						SourceEvent: TelegramCallbackEvent.MyConnections,
						Page: page)))));
	}
}