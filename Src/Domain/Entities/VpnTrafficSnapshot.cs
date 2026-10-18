using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Снимок счётчиков трафика клиента — нужен для расчёта статистики за периоды,
/// т.к. счётчики 3x-ui обнуляются при сбросе трафика или пересоздании клиента.
/// </summary>
public class VpnTrafficSnapshot : IEntity
{
    public int Id { get; init; }

    /// <summary>
    /// Идентификатор клиента в 3x-ui (UUID VLESS)
    /// </summary>
    public Guid ClientId { get; init; }

    /// <summary>
    /// Когда снят снимок
    /// </summary>
    public DateTime CapturedAtUtc { get; init; }

    /// <summary>
    /// Байт получено сервером от клиента (нарастающий итог с последнего рестарта интерфейса)
    /// </summary>
    public long TransferRx { get; init; }

    /// <summary>
    /// Байт отправлено сервером клиенту (нарастающий итог с последнего рестарта интерфейса)
    /// </summary>
    public long TransferTx { get; init; }
}
