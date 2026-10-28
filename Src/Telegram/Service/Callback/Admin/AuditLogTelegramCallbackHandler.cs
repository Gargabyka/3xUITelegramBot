using System.Text;
using Domain.Extensions;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Журнал действий <see cref="TelegramCallbackEvent.AuditLog"/>, новые записи первыми
/// </summary>
[UsedImplicitly]
public sealed class AuditLogTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private const int PageSize = 10;

	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;

	public AuditLogTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.AuditLog;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			await DenyAsync(update, "Доступно только администраторам.", cancellationToken);
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);

		using var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

		var total = await repo.CountAsync(cancellationToken);
		var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
		var page = Math.Clamp(request.Page, 0, totalPages - 1);

		var entries = await repo.GetPageAsync(page * PageSize, PageSize, cancellationToken);

		var text = new StringBuilder($"📜 Журнал действий (стр. {page + 1}/{totalPages}, всего {total}):");
		if (entries.Length == 0)
		{
			text.Append("\n\nЗаписей пока нет.");
		}

		foreach (var entry in entries)
		{
			var at = DateTime.SpecifyKind(entry.CreatedAtUtc, DateTimeKind.Utc).ToLocalTime();
			var actor = entry.ActorName ?? $"id {entry.ActorTelegramId}";

			text.Append($"\n\n{at:dd.MM.yyyy HH:mm} · {actor}\n{entry.Action.GetDescription()}: {entry.Details}");
		}

		var navigation = new List<InlineKeyboardButton>();
		if (page > 0)
		{
			navigation.Add(InlineKeyboardButton.WithCallbackData("⬅", Encode(ToPage(page - 1))));
		}

		if (page < totalPages - 1)
		{
			navigation.Add(InlineKeyboardButton.WithCallbackData("➡", Encode(ToPage(page + 1))));
		}

		var rows = new List<InlineKeyboardButton[]>();
		if (navigation.Count > 0)
		{
			rows.Add(navigation.ToArray());
		}

		rows.Add([InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(BackTo(TelegramCallbackEvent.AdminPanel)))]);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: text.ToString(),
			replyMarkup: new InlineKeyboardMarkup(rows),
			cancellationToken: cancellationToken);

		await AnswerAsync(update, cancellationToken);
	}

	private static TelegramRequest ToPage(int page)
		=> new(Guid.Empty, TelegramCallbackEvent.AuditLog, TelegramCallbackEvent.AdminPanel, page);
}
