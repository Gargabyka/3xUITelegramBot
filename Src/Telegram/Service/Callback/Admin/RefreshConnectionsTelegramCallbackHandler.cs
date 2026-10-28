using Application.Services;
using Domain.Enums;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Enums;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

[UsedImplicitly]
public sealed class RefreshConnectionsTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly AuditLogger _audit;

	public RefreshConnectionsTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		AuditLogger audit)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
		_audit = audit;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.RefreshConnections;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var chatId = update.CallbackQuery?.Message?.Chat.Id;
		if (chatId is null)
		{
			return;
		}

		if (!await IsAdminAsync(chatId.Value, cancellationToken))
		{
			return;
		}

		await _telegramBot.SendMessage(
			chatId: chatId.Value,
			text: "Обновляю соединения из 3x-ui…",
			cancellationToken: cancellationToken);

		try
		{
			using var scope = _scopeFactory.CreateScope();
			var syncService = scope.ServiceProvider.GetRequiredService<PeerSyncService>();

			var result = await syncService.SyncAsync(cancellationToken);

			await _audit.LogAsync(
				chatId.Value,
				AuditAction.ConnectionsSynced,
				$"добавлено {result.Added}, обновлено {result.Updated}, удалено {result.Removed}",
				cancellationToken);

			var text = $"""
			            Готово.
			            Всего на серверах: {result.Total}
			            Добавлено: {result.Added}
			            Обновлено: {result.Updated}
			            Удалено (нет на сервере): {result.Removed}
			            """;

			if (result.UnavailableProfiles.Length > 0)
			{
				text += $"\n\n⚠ Не ответили: {string.Join(", ", result.UnavailableProfiles)} — их соединения не трогались.";
			}

			await _telegramBot.SendMessage(
				chatId: chatId.Value,
				text: text,
				cancellationToken: cancellationToken);
		}
		catch (OperationCanceledException)
		{
			// отмена — не ошибка
			await _telegramBot.SendMessage(
				chatId: chatId.Value,
				text: "Операция отменена.",
				cancellationToken: cancellationToken);
		}
		catch (Exception)
		{
			// Не светим детали исключения в чат
			await _telegramBot.SendMessage(
				chatId: chatId.Value,
				text: "Не удалось обновить соединения. Проверьте логи приложения.",
				cancellationToken: cancellationToken);
		}
	}
}