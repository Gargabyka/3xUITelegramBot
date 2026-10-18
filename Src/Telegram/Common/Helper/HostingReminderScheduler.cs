namespace Telegram.Common.Helper;

/// <summary>
/// Считает дату очередного напоминания об оплате хостинга.
/// </summary>
/// <remarks>
/// AddMonths всегда применяется к исходной дате оплаты (а не к ранее вычисленной дате) —
/// иначе для дат конца месяца (29/30/31) после пересечения более короткого месяца
/// (например, февраля) дата напоминания необратимо "уезжает" на более раннее число.
/// </remarks>
public static class HostingReminderScheduler
{
	/// <summary>
	/// Дата <paramref name="cycleNumber"/>-го напоминания после <paramref name="lastPaymentDate"/>
	/// </summary>
	public static DateOnly GetDueDate(DateOnly lastPaymentDate, int periodMonths, int cycleNumber)
		=> lastPaymentDate.AddMonths(periodMonths * cycleNumber);
}
