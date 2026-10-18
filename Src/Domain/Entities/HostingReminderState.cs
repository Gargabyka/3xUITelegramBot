using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Состояние рассылки напоминаний об оплате хостинга (единственная строка в таблице)
/// </summary>
public class HostingReminderState : IEntity
{
    public int Id { get; init; }

    /// <summary>
    /// Сколько циклов напоминаний уже отправлено (0 — ещё ни одного).
    /// Дата следующего напоминания считается от даты последней оплаты как
    /// LastPaymentDate + (RemindersSentCount + 1) * PeriodMonths — всегда от исходной даты,
    /// а не цепочкой AddMonths, чтобы не "плыть" для дат конца месяца.
    /// </summary>
    public int RemindersSentCount { get; set; }

    /// <summary>
    /// Когда последний раз фактически отправляли напоминание (для информации/отладки)
    /// </summary>
    public DateTime? LastSentAtUtc { get; set; }
}
