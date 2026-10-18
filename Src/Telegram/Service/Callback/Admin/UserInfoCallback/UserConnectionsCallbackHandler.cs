using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using XUi.Models;
using XUi.Services.Interface;

namespace Telegram.Service.Callback.Admin.UserInfoCallback;

[UsedImplicitly]
public class UserConnectionsCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private const int PageSize = 9;

	private readonly ITelegramBotClient _bot;
	private readonly IXUiApiService _xuiApiService;
	private readonly IServiceScopeFactory _scopeFactory;

	public UserConnectionsCallbackHandler(
		ITelegramBotClient telegramBot,
		IXUiApiService xuiApiService,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_bot = telegramBot;
		_xuiApiService = xuiApiService;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.UserConnections;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		var telegramId = LongGuidCodec.Decode(request.Id);

		using var scope = _scopeFactory.CreateScope();

		var userRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
		var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();

		var user = await userRepo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
		if (user is null)
		{
			var backMissing = BackTo(TelegramCallbackEvent.UserList, sourceEvent: TelegramCallbackEvent.AdminPanel);

			await _bot.SendMessage(
				chatId: chatId,
				text: "Пользователь не найден.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад к списку", Encode(backMissing))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var connections = await vpnUserRepo.GetConnectionsByTelegramIdAsync(user.Id, cancellationToken);

		var userNameSuffix = string.IsNullOrWhiteSpace(user.UserName)
			? string.Empty
			: $" (@{user.UserName.Trim().TrimStart('@')})";

		if (connections.Length == 0)
		{
			var backToUserInfoEmpty = new TelegramRequest(
				Id: request.Id,
				CallbackEvent: TelegramCallbackEvent.UserInfo,
				SourceEvent: TelegramCallbackEvent.UserConnections);

			await _bot.SendMessage(
				chatId: chatId,
				text: $"У пользователя {user.TelegramId}{userNameSuffix} нет соединений.",
				replyMarkup: new InlineKeyboardMarkup(
					InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(backToUserInfoEmpty))),
				cancellationToken: cancellationToken);

			await AnswerAsync(update, cancellationToken);
			return;
		}

		var ordered = connections
			.OrderByDescending(x => x.Peer.CreatedAt)
			.ToArray();

		// Трафик берём live из 3x-ui API — если он недоступен, просто не показываем статистику
		Dictionary<Guid, XUiClient> clientsById = [];
		try
		{
			var clients = await _xuiApiService.GetClients(cancellationToken) ?? [];
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

		var rows = BuildPeerRows(pageConnections, request.Id, page, clientsById);

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
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserConnections,
			SourceEvent: TelegramCallbackEvent.UserInfo,
			Page: prevPage);

		var navNext = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserConnections,
			SourceEvent: TelegramCallbackEvent.UserInfo,
			Page: nextPage);

		rows.Add([
			InlineKeyboardButton.WithCallbackData(page > 0 ? "⬅️" : " ", Encode(navPrev)),
			InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", Encode(new TelegramRequest(request.Id, TelegramCallbackEvent.UserConnections, TelegramCallbackEvent.UserInfo, page))),
			InlineKeyboardButton.WithCallbackData(page < totalPages - 1 ? "➡️" : " ", Encode(navNext)),
		]);

		var backToUserInfo = new TelegramRequest(
			Id: request.Id,
			CallbackEvent: TelegramCallbackEvent.UserInfo,
			SourceEvent: TelegramCallbackEvent.UserConnections);

		rows.Add([InlineKeyboardButton.WithCallbackData("⬅ Назад к пользователю", Encode(backToUserInfo))]);

		var trafficLine = $"Всего использовано трафика: {TrafficFormatter.FormatBytes(totalBytes)}";

		await _bot.SendMessage(
			chatId: chatId,
			text: $"""
			       Соединения пользователя{userNameSuffix} (нажмите на имя для подробностей):
			       {trafficLine}
			       """,
			replyMarkup: new InlineKeyboardMarkup(rows),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}

	private static List<InlineKeyboardButton[]> BuildPeerRows(
		IEnumerable<Domain.Entities.VpnUser> connections,
		Guid userRequestId,
		int page,
		IReadOnlyDictionary<Guid, XUiClient> clientsById)
	{
		var rows = new List<InlineKeyboardButton[]>();

		foreach (var c in connections)
		{
			var peer = c.Peer;

			var statusIcon = clientsById.TryGetValue(peer.ClientId, out var client)
				? (client.Enabled ? "🟢 " : "🔴 ")
				: string.Empty;

			var infoBtn = InlineKeyboardButton.WithCallbackData(
				text: statusIcon + peer.Name,
				callbackData: TelegramCallbackDataCodec.Encode(
					new TelegramRequest(
						Id: peer.ClientId,
						CallbackEvent: TelegramCallbackEvent.GetFullInfo,
						SourceEvent: TelegramCallbackEvent.UserConnections,
						Page: page)));

			var unlinkBtn = InlineKeyboardButton.WithCallbackData(
				text: "⛓‍💥 Разорвать",
				callbackData: TelegramCallbackDataCodec.Encode(
					new TelegramRequest(
						Id: userRequestId,
						CallbackEvent: TelegramCallbackEvent.UserUnlinkConnection,
						SourceEvent: TelegramCallbackEvent.UserConnections,
						Page: peer.Id)));

			// В одну строку: слева соединение, справа разорвать
			rows.Add([infoBtn, unlinkBtn]);
		}

		return rows;
	}
}