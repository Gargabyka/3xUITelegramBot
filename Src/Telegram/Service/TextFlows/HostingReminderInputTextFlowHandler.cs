using System.Globalization;
using Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Common.Helper;
using Telegram.Service.Interface;
using Telegram.Store;

namespace Telegram.Service.TextFlows;

/// <summary>
/// Ввод даты оплаты и текста напоминания на экране «Оплата сервера»
/// </summary>
public sealed class HostingReminderInputTextFlowHandler : ITelegramTextFlowHandler
{
	private static readonly string[] DateFormats = ["dd.MM.yyyy", "d.M.yyyy"];

	private readonly ITelegramBotClient _bot;
	private readonly ExpiringStateStore<HostingReminderInputState> _inputs;
	private readonly HostingReminderService _reminders;
	private readonly TelegramAccessService _access;
	private readonly AuditLogger _audit;

	public HostingReminderInputTextFlowHandler(
		ITelegramBotClient bot,
		ExpiringStateStore<HostingReminderInputState> inputs,
		HostingReminderService reminders,
		TelegramAccessService access,
		AuditLogger audit)
	{
		_bot = bot;
		_inputs = inputs;
		_reminders = reminders;
		_access = access;
		_audit = audit;
	}

	public async Task<bool> TryHandleAsync(Update update, CancellationToken cancellationToken)
	{
		var message = update.Message;
		if (message is null || message.Text?.StartsWith('/') == true)
		{
			return false;
		}

		var chatId = message.Chat.Id;
		if (!_inputs.TryGet(chatId, out var input))
		{
			return false;
		}

		if (!await _access.IsAdminAsync(chatId, cancellationToken))
		{
			_inputs.Clear(chatId);
			return false;
		}

		return input.Kind switch
		{
			HostingReminderInputKind.PaymentDate => await HandlePaymentDateAsync(chatId, message, cancellationToken),
			HostingReminderInputKind.Message => await HandleMessageAsync(chatId, message, cancellationToken),
			_ => false,
		};
	}

	private async Task<bool> HandlePaymentDateAsync(long chatId, Message message, CancellationToken cancellationToken)
	{
		var text = message.Text?.Trim();
		if (!DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
		{
			await _bot.SendMessage(chatId, "Не понял дату. Пришлите в формате ДД.ММ.ГГГГ, например 19.09.2026, или нажмите «Отмена».", cancellationToken: cancellationToken);
			return true;
		}

		if (date > DateOnly.FromDateTime(DateTime.UtcNow))
		{
			await _bot.SendMessage(chatId, "Дата оплаты не может быть в будущем. Пришлите другую или нажмите «Отмена».", cancellationToken: cancellationToken);
			return true;
		}

		_inputs.Clear(chatId);

		await _reminders.UpdateAsync(s => s.LastPaymentDate = date, restartSchedule: true, cancellationToken);
		await _audit.LogAsync(chatId, AuditAction.HostingReminderChanged, $"дата оплаты: {date:dd.MM.yyyy}", cancellationToken);

		await HostingReminderScreen.SendAsync(_bot, _reminders, chatId, $"📅 Дата оплаты: {date:dd.MM.yyyy}.", cancellationToken);
		return true;
	}

	private async Task<bool> HandleMessageAsync(long chatId, Message message, CancellationToken cancellationToken)
	{
		// Текст сохраняем как запасной вариант: если сообщение-образец удалят, бот отправит хотя бы его
		var fallbackText = message.Text ?? message.Caption;
		if (string.IsNullOrWhiteSpace(fallbackText))
		{
			await _bot.SendMessage(chatId, "В напоминании должен быть текст (у фото — подпись). Пришлите другое сообщение или нажмите «Отмена».", cancellationToken: cancellationToken);
			return true;
		}

		_inputs.Clear(chatId);

		await _reminders.UpdateAsync(
			s =>
			{
				s.MessageChatId = chatId;
				s.MessageId = message.MessageId;
				s.MessageText = fallbackText;
			},
			restartSchedule: false,
			cancellationToken);

		await _audit.LogAsync(chatId, AuditAction.HostingReminderChanged, "изменён текст напоминания", cancellationToken);

		await HostingReminderScreen.SendAsync(
			_bot,
			_reminders,
			chatId,
			"✏ Текст напоминания сохранён. Проверить, как его увидят пользователи, — «👁 Предпросмотр».",
			cancellationToken);
		return true;
	}
}
