namespace XUi.Models;

/// <summary>
/// Клиенты 3x-ui
/// </summary>
/// <param name="Id">Идентификатор пользователя</param>
/// <param name="Name">Именование пользователя</param>
/// <param name="Enabled">Признак, что клиент включен</param>
/// <param name="Address">Id адрес</param>
/// <param name="PublicKey">Публичный ключ</param>
/// <param name="CreatedAt">Дата создания</param>
/// <param name="LatestHandshakeAt">Дата последнего подключения (null — ни разу не подключался)</param>
/// <param name="TransferRx">Байт получено сервером от клиента</param>
/// <param name="TransferTx">Байт отправлено сервером клиенту</param>
public record XUiClient(
    Guid Id,
    string Name,
    bool Enabled,
    string Address,
    string PublicKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LatestHandshakeAt,
    long TransferRx,
    long TransferTx);
