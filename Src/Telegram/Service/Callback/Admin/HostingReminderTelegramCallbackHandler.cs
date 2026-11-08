using Domain.Enums;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Common.Helper;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Store;
using Screen = Telegram.Common.Helper.HostingReminderScreen;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Экран «Оплата сервера» и его действия <see cref="TelegramCallbackEvent.HostingReminder"/> (код действия в Page)
/// </summary>
[UsedImplicitly]
public sealed class HostingReminderTelegramCallbackHandler : TelegramAdminCallbackHandlerBase
{
	private readonly ITelegramBotClient _telegramBot;
	private readonly HostingReminderService _reminders;
	private readonly ExpiringStateStore<HostingReminderInputState> _inputs;
	private readonly AuditLogger _audit;

	public HostingReminderTelegramCallbackHandler(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory,
		HostingReminderService reminders,
		ExpiringStateStore<HostingReminderInputState> inputs,
		AuditLogger audit)
		: base(telegramBot, telegramOptions, scopeFactory)
	{
		_telegramBot = telegramBot;
		_reminders = reminders;
		_inputs = inputs;
		_audit = audit;
	}

	public override TelegramCallbackEvent CallbackEvent => TelegramCallbackEvent.HostingReminder;

	public override async Task CallbackHandlerAsync(Update update, TelegramRequest request, CancellationToken cancellationToken)
	{
		var (chatId, _, _) = GetCallbackContext(update);

		if (!await IsAdminAsync(chatId, cancellationToken))
		{
			await DenyAsync(update, "Доступно только администраторам.", cancellationToken);
			return;
		}

		// Предпросмотр — отдельным сообщением, экран настроек остаётся на месте
		if (request.Page == Screen.Preview)
		{
			await PreviewAsync(update, chatId, cancellationToken);
			return;
		}

		await TryDeleteOriginMessageAsync(update, cancellationToken);
		await AnswerAsync(update, cancellationToken);

		switch (request.Page)
		{
			case Screen.TogglePaused:
				await ToggleAsync(chatId, cancellationToken);
				break;

			case Screen.MarkPaid:
				await MarkPaidAsync(chatId, cancellationToken);
				break;

			case Screen.AskSendNow:
				await AskSendNowAsync(chatId, cancellationToken);
				break;

			case Screen.ConfirmSendNow:
				await SendNowAsync(chatId, cancellationToken);
				break;

			case Screen.EnterPaymentDate:
				await AskInputAsync(
					chatId,
					HostingReminderInputKind.PaymentDate,
					"📅 Пришлите дату последней оплаты в формате ДД.ММ.ГГГГ, например 19.09.2026.\nОт неё будут считаться следующие сроки.",
					cancellationToken);
				break;

			case Screen.EnterMessage:
				await AskInputAsync(
					chatId,
					HostingReminderInputKind.Message,
					"""
					✏ Пришлите текст напоминания одним сообщением — с форматированием, можно с фото.
					Пользователи получат его копию как есть.

					Не удаляйте это сообщение из чата: напоминание копируется из него. Если оно пропадёт, бот отправит его текст без форматирования.
					""",
					cancellationToken);
				break;

			case Screen.ChoosePeriod:
				await ChoosePeriodAsync(chatId, cancellationToken);
				break;

			case > Screen.SetPeriodBase when HostingReminderService.PeriodPresets.Contains(request.Page - Screen.SetPeriodBase):
				await SetPeriodAsync(chatId, request.Page - Screen.SetPeriodBase, cancellationToken);
				break;

			default:
				await Screen.SendAsync(_telegramBot, _reminders, chatId, notice: null, cancellationToken);
				break;
		}
	}

	private async Task PreviewAsync(Update update, long chatId, CancellationToken cancellationToken)
	{
		if (!await _reminders.SendPreviewAsync(chatId, cancellationToken))
		{
			await DenyAsync(update, "Текст напоминания не задан.", cancellationToken);
			return;
		}

		await AnswerAsync(update, cancellationToken);
	}

	private async Task ToggleAsync(long chatId, CancellationToken cancellationToken)
	{
		var enabled = false;
		await _reminders.UpdateAsync(s => enabled = s.Enabled = !s.Enabled, restartSchedule: false, cancellationToken);
		await _audit.LogAsync(chatId, AuditAction.HostingReminderChanged, enabled ? "напоминания включены" : "напоминания выключены", cancellationToken);

		await Screen.SendAsync(_telegramBot, _reminders, chatId, enabled ? "▶ Напоминания включены." : "⏸ Напоминания выключены.", cancellationToken);
	}

	private async Task MarkPaidAsync(long chatId, CancellationToken cancellationToken)
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		await _reminders.UpdateAsync(s => s.LastPaymentDate = today, restartSchedule: true, cancellationToken);
		await _audit.LogAsync(chatId, AuditAction.HostingPaid, $"оплачено {today:dd.MM.yyyy}", cancellationToken);

		await Screen.SendAsync(_telegramBot, _reminders, chatId, $"✅ Оплата отмечена: {today:dd.MM.yyyy}.", cancellationToken);
	}

	private async Task AskSendNowAsync(long chatId, CancellationToken cancellationToken)
	{
		var (recipients, _) = await _reminders.CountRecipientsAsync(cancellationToken);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: $"Разослать напоминание об оплате сейчас? Получателей: {recipients}. Расписание при этом не сдвинется.",
			replyMarkup: new InlineKeyboardMarkup([
				[InlineKeyboardButton.WithCallbackData($"✅ Разослать ({recipients})", Encode(Screen.Action(Screen.ConfirmSendNow)))],
				[InlineKeyboardButton.WithCallbackData("⬅ Отмена", Encode(Screen.Action(Screen.Show)))],
			]),
			cancellationToken: cancellationToken);
	}

	private async Task SendNowAsync(long chatId, CancellationToken cancellationToken)
	{
		var settings = await _reminders.GetAsync(cancellationToken);
		if (!settings.HasMessage)
		{
			await Screen.SendAsync(_telegramBot, _reminders, chatId, "⚠ Сначала задайте текст напоминания.", cancellationToken);
			return;
		}

		await _telegramBot.SendMessage(chatId, "Отправляю напоминание…", cancellationToken: cancellationToken);

		var (sent, failed) = await _reminders.SendNowAsync(cancellationToken);
		await _audit.LogAsync(chatId, AuditAction.HostingReminderSent, $"доставлено {sent}, не доставлено {failed}", cancellationToken);

		var notice = failed == 0
			? $"📣 Напоминание разослано: {sent}."
			: $"📣 Напоминание разослано: доставлено {sent}, не доставлено {failed} (например, бот заблокирован пользователем).";

		await Screen.SendAsync(_telegramBot, _reminders, chatId, notice, cancellationToken);
	}

	private async Task AskInputAsync(long chatId, HostingReminderInputKind kind, string prompt, CancellationToken cancellationToken)
	{
		_inputs.Set(chatId, new HostingReminderInputState(kind));

		var cancelRequest = new TelegramRequest(Guid.Empty, TelegramCallbackEvent.CancelInput, TelegramCallbackEvent.AdminPanel);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: prompt,
			replyMarkup: new InlineKeyboardMarkup(
				InlineKeyboardButton.WithCallbackData("❌ Отмена", Encode(cancelRequest))),
			cancellationToken: cancellationToken);
	}

	private async Task ChoosePeriodAsync(long chatId, CancellationToken cancellationToken)
	{
		var current = (await _reminders.GetAsync(cancellationToken)).PeriodMonths;

		var presets = HostingReminderService.PeriodPresets
			.Select(months => InlineKeyboardButton.WithCallbackData(
				(months == current ? "✅ " : string.Empty) + $"{months} мес.",
				Encode(Screen.Action(Screen.SetPeriodBase + months))))
			.Chunk(3);

		await _telegramBot.SendMessage(
			chatId: chatId,
			text: "🔁 Как часто нужно платить за сервер?",
			replyMarkup: new InlineKeyboardMarkup([
				.. presets,
				[InlineKeyboardButton.WithCallbackData("⬅ Назад", Encode(Screen.Action(Screen.Show)))],
			]),
			cancellationToken: cancellationToken);
	}

	private async Task SetPeriodAsync(long chatId, int months, CancellationToken cancellationToken)
	{
		await _reminders.UpdateAsync(s => s.PeriodMonths = months, restartSchedule: true, cancellationToken);
		await _audit.LogAsync(chatId, AuditAction.HostingReminderChanged, $"период оплаты: {months} мес.", cancellationToken);

		await Screen.SendAsync(_telegramBot, _reminders, chatId, $"🔁 Период: каждые {months} мес.", cancellationToken);
	}
}
