using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Enums;
using Telegram.Models;
using Telegram.Service;

namespace Telegram.Common.Helper;

/// <summary>
/// Экран «Оплата сервера» в админ-панели. Действие экрана передаётся в Page события
/// <see cref="TelegramCallbackEvent.HostingReminder"/>
/// </summary>
public static class HostingReminderScreen
{
	public const int Show = 0;
	public const int TogglePaused = 1;
	public const int MarkPaid = 2;
	public const int Preview = 3;
	public const int AskSendNow = 4;
	public const int ConfirmSendNow = 5;
	public const int EnterPaymentDate = 6;
	public const int EnterMessage = 7;
	public const int ChoosePeriod = 8;

	/// <summary>
	/// Выбор периода: Page = SetPeriodBase + число месяцев
	/// </summary>
	public const int SetPeriodBase = 100;

	public static TelegramRequest Action(int action)
		=> new(Guid.Empty, TelegramCallbackEvent.HostingReminder, TelegramCallbackEvent.AdminPanel, action);

	/// <summary>
	/// Прислать экран с текущими настройками
	/// </summary>
	/// <param name="notice">Строка о результате последнего действия (например, «Оплата отмечена»)</param>
	public static async Task SendAsync(
		ITelegramBotClient bot,
		HostingReminderService reminders,
		long chatId,
		string? notice,
		CancellationToken cancellationToken)
	{
		var settings = await reminders.GetAsync(cancellationToken);
		var (recipients, total) = await reminders.CountRecipientsAsync(cancellationToken);
		var today = DateOnly.FromDateTime(DateTime.UtcNow);

		var nextLine = settings.NextDueDate switch
		{
			null => "не задана дата оплаты",
			{ } due when due <= today => $"{due:dd.MM.yyyy} (сегодня — уйдёт в течение часа)",
			{ } due => $"{due:dd.MM.yyyy} (через {due.DayNumber - today.DayNumber} дн.)",
		};

		var messageLine = settings switch
		{
			{ HasMessage: false } => "⚠ не задан — напоминания не уходят",
			{ IsCopiedMessage: true } => "задан (сообщение-образец, с форматированием)",
			_ => "задан",
		};

		var text = $"""
		            💰 Оплата сервера{(notice is null ? string.Empty : $"\n\n{notice}")}

		            Напоминания: {(settings.Enabled ? "✅ включены" : "⏸ выключены")}
		            Последняя оплата: {settings.LastPaymentDate?.ToString("dd.MM.yyyy") ?? "не задана"}
		            Период: каждые {settings.PeriodMonths} мес.
		            Следующее напоминание: {(settings.Enabled ? nextLine : "—")}
		            Текст: {messageLine}
		            Получатели: {recipients} из {total} (остальным админ отключил напоминания)
		            """;

		var markup = new InlineKeyboardMarkup([
			[
				Button("✅ Я оплатил", MarkPaid),
				Button(settings.Enabled ? "⏸ Выключить" : "▶ Включить", TogglePaused),
			],
			[
				Button("📅 Дата оплаты", EnterPaymentDate),
				Button("🔁 Период", ChoosePeriod),
			],
			[
				Button("✏ Текст напоминания", EnterMessage),
				Button("👁 Предпросмотр", Preview),
			],
			[Button("📣 Отправить сейчас", AskSendNow)],
			[InlineKeyboardButton.WithCallbackData(
				"⬅ Назад",
				TelegramCallbackDataCodec.Encode(new TelegramRequest(Guid.Empty, TelegramCallbackEvent.AdminPanel, TelegramCallbackEvent.AdminPanel)))],
		]);

		await bot.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: cancellationToken);
	}

	private static InlineKeyboardButton Button(string text, int action)
		=> InlineKeyboardButton.WithCallbackData(text, TelegramCallbackDataCodec.Encode(Action(action)));
}
