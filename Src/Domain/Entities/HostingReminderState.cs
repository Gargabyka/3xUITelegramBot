using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Напоминания об оплате хостинга: настройки и состояние рассылки (единственная строка в таблице).
/// Настройки задаются только из админ-панели бота
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

    /// <summary>
    /// Рассылать напоминания
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Дата последней оплаты — точка отсчёта циклов (null — не задана, напоминания не уходят)
    /// </summary>
    public DateOnly? LastPaymentDate { get; set; }

    /// <summary>
    /// Раз во сколько месяцев нужно платить
    /// </summary>
    public int PeriodMonths { get; set; } = 2;

    /// <summary>
    /// Текст напоминания. Если задано сообщение-образец, текст — запасной вариант на случай, когда образец удалён
    /// </summary>
    public string? MessageText { get; set; }

    /// <summary>
    /// Чат сообщения-образца: напоминание копируется из него как есть, с форматированием и вложениями
    /// </summary>
    public long? MessageChatId { get; set; }

    /// <summary>
    /// Идентификатор сообщения-образца в <see cref="MessageChatId"/>
    /// </summary>
    public int? MessageId { get; set; }
}
