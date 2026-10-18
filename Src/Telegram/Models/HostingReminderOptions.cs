namespace Telegram.Models;

/// <summary>
/// Настройки периодических напоминаний об оплате хостинга
/// </summary>
public sealed class HostingReminderOptions
{
    /// <summary>
    /// Включить рассылку напоминаний
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Дата последней оплаты — точка отсчёта цикла (например, 2026-09-19)
    /// </summary>
    public required DateOnly LastPaymentDate { get; init; }

    /// <summary>
    /// Раз во сколько месяцев нужно платить (считается от <see cref="LastPaymentDate"/>)
    /// </summary>
    public int PeriodMonths { get; init; } = 2;

    /// <summary>
    /// Текст напоминания (с реквизитами)
    /// </summary>
    public required string Message { get; init; }
}
