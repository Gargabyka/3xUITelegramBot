using Domain.Enums;
using Domain.Interfaces.Repository;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Отправка подтверждённой рассылки <see cref="TelegramCallbackEvent.BroadcastSend"/>
/// </summary>
[UsedImplicitly]
public sealed class BroadcastSendTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	// Лимит Telegram — около 30 сообщений в секунду; держимся с запасом
	private static readonly TimeSpan DelayBetweenMessages = TimeSpan.FromMilliseconds(50);

	private readonly ITelegramBotClient _telegramBot;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ExpiringStateStore<BroadcastState> _store;
	private readonly AuditLogger _audit;
	private readonly ILogger<BroadcastSendTelegramCallbackHandler> _logger;

	public BroadcastSendTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		ExpiringStateStore<BroadcastState> store,
		AuditLogger audit,
		ILogger<BroadcastSendTelegramCallbackHandler> logger)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_scopeFactory = scopeFactory;
		_store = store;
		_audit = audit;
		_logger = logger;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.BroadcastSend;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			await DenyAsync(update, "Доступно только администраторам.", cancellationToken);
			return;
		}

		// Повторное нажатие или устаревшая кнопка не должны отправить рассылку второй раз
		if (!_store.TryGet(chatId, out var state) || state.MessageId is not { } messageId)
		{
			await DenyAsync(update, "Рассылка уже отправлена или отменена.", cancellationToken);
			return;
		}

		_store.Clear(chatId);
		await TryDeleteOriginMessageAsync(update, cancellationToken);
		await AnswerAsync(update, cancellationToken);

		long[] recipients;
		using (var scope = _scopeFactory.CreateScope())
		{
			var users = await scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>().GetUsersAsync(cancellationToken);
			recipients = users.Select(x => x.TelegramId).Where(x => x != chatId).ToArray();
		}

		await _telegramBot.SendMessage(chatId, $"Отправляю рассылку: {recipients.Length} получателей…", cancellationToken: cancellationToken);

		var sent = 0;
		var failed = 0;
		foreach (var recipient in recipients)
		{
			try
			{
				await _telegramBot.CopyMessage(recipient, chatId, messageId, cancellationToken: cancellationToken);
				sent++;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Пользователь мог заблокировать бота — это не повод останавливать рассылку
				failed++;
				_logger.LogWarning(ex, "Не удалось доставить рассылку пользователю {TelegramId}", recipient);
			}

			await Task.Delay(DelayBetweenMessages, cancellationToken);
		}

		await _audit.LogAsync(chatId, AuditAction.Broadcast, $"доставлено {sent}, не доставлено {failed}", cancellationToken);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: failed == 0
				? $"✅ Рассылка доставлена: {sent}."
				: $"Рассылка завершена: доставлено {sent}, не доставлено {failed} (например, бот заблокирован пользователем).",
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("⬅ В админ панель", Encode(BackTo(TelegramCallbackEvent.AdminPanel)))),
			cancellationToken: cancellationToken);
	}
}
