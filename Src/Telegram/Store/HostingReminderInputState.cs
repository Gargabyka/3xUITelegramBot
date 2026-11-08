namespace Telegram.Store;

/// <summary>
/// Что бот ждёт от админа на экране «Оплата сервера»
/// </summary>
/// <param name="Kind">Дата оплаты или текст напоминания</param>
public sealed record HostingReminderInputState(HostingReminderInputKind Kind);

public enum HostingReminderInputKind
{
	/// <summary>
	/// Дата последней оплаты
	/// </summary>
	PaymentDate = 1,

	/// <summary>
	/// Сообщение-образец напоминания
	/// </summary>
	Message = 2,
}
