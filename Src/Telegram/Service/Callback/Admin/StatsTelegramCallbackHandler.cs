using System.Text;
using Domain.Entities;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Статистика: кто сейчас онлайн и топ соединений по трафику <see cref="TelegramCallbackEvent.Stats"/>
/// </summary>
[UsedImplicitly]
public sealed class StatsTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private const int TopSize = 10;
	private const int MaxOnlineLines = 40;

	// Панели обновляют время последней активности раз в несколько секунд, рукопожатие WireGuard — раз в 2 минуты
	private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(3);
	private static readonly TimeSpan TopPeriod = TimeSpan.FromDays(30);

	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IVpnApiService _vpnApiService;

	public StatsTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		IVpnApiService vpnApiService)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
		_vpnApiService = vpnApiService;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.Stats;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			await DenyAsync(update, "Доступно только администраторам.", cancellationToken);
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);
		await AnswerAsync(update, cancellationToken);

		var statuses = await _vpnApiService.GetStatusAsync(cancellationToken);
		var clients = statuses
			.SelectMany(x => x.Clients)
			.GroupBy(x => x.Id)
			.Select(x => x.First())
			.ToArray();

		using var scope = _scopeFactory.CreateScope();
		var links = await scope.ServiceProvider.GetRequiredService<IVpnUserRepository>().GetAllLinksAsync(cancellationToken);
		var owners = links
			.GroupBy(x => x.Peer.ClientId)
			.ToDictionary(x => x.Key, x => string.Join(", ", x.Select(l => FormatOwner(l.Telegram))));

		var text = new StringBuilder();
		AppendOnline(text, clients, owners);
		text.Append("\n\n");
		await AppendTopAsync(text, clients, owners, scope.ServiceProvider, cancellationToken);

		var unavailable = statuses.Where(x => !x.IsAvailable).Select(x => x.Profile.Title).ToArray();
		if (unavailable.Length > 0)
		{
			text.Append($"\n\n⚠ Не ответили: {string.Join(", ", unavailable)} — их соединения не учтены.");
		}

		var refresh = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.Stats, TelegramCallbackEvent.AdminPanel);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: text.ToString(),
			replyMarkup: new InlineKeyboardMarkup([
				[InlineKeyboardButton.WithCallbackData("🔄 Обновить", Encode(refresh))],
				[InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(BackTo(TelegramCallbackEvent.AdminPanel)))],
			]),
			cancellationToken: cancellationToken);
	}

	private static string FormatOwner(TelegramUser user)
		=> string.IsNullOrWhiteSpace(user.UserName)
			? $"{user.FirstName} {user.LastName}".Trim()
			: $"@{user.UserName}";

	private static string FormatClient(VpnServerClient client, Dictionary<Guid, string> owners)
		=> owners.TryGetValue(client.Id, out var owner)
			? $"{client.Name} [{client.Profile.Title()}] — {owner}"
			: $"{client.Name} [{client.Profile.Title()}]";

	private static void AppendOnline(StringBuilder text, VpnServerClient[] clients, Dictionary<Guid, string> owners)
	{
		var since = DateTimeOffset.UtcNow - OnlineWindow;
		var online = clients
			.Where(x => x.LatestHandshakeAt >= since)
			.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		text.Append($"🟢 Сейчас онлайн: {online.Length} из {clients.Length}");
		foreach (var client in online.Take(MaxOnlineLines))
		{
			text.Append($"\n• {FormatClient(client, owners)}");
		}

		// Сообщение Telegram ограничено 4096 символами
		if (online.Length > MaxOnlineLines)
		{
			text.Append($"\n…и ещё {online.Length - MaxOnlineLines}");
		}
	}

	private static async Task AppendTopAsync(
		StringBuilder text,
		VpnServerClient[] clients,
		Dictionary<Guid, string> owners,
		IServiceProvider services,
		CancellationToken cancellationToken)
	{
		var snapshotRepo = services.GetRequiredService<IVpnTrafficSnapshotRepository>();
		var since = DateTime.UtcNow - TopPeriod;

		// Запросы последовательно — DbContext не потокобезопасен
		var usage = new List<(VpnServerClient Client, long Bytes)>();
		foreach (var client in clients)
		{
			var snapshots = await snapshotRepo.GetSnapshotsForPeriodAsync(client.Id, since, cancellationToken);
			var (rx, tx) = TrafficUsageCalculator.ComputeUsage(snapshots);
			if (rx + tx > 0)
			{
				usage.Add((client, rx + tx));
			}
		}

		var top = usage.OrderByDescending(x => x.Bytes).Take(TopSize).ToArray();

		text.Append($"🏆 Топ по трафику за 30 дней (всего {TrafficFormatter.FormatBytes(usage.Sum(x => x.Bytes))}):");
		if (top.Length == 0)
		{
			text.Append("\nПока нет данных — снимки трафика собираются раз в час.");
			return;
		}

		for (var i = 0; i < top.Length; i++)
		{
			text.Append($"\n{i + 1}. {FormatClient(top[i].Client, owners)}: {TrafficFormatter.FormatBytes(top[i].Bytes)}");
		}
	}
}
