namespace Telegram.Store.Interfaces;

/// <summary>
/// Хранилище состояния сценария добавления клиента для конкретного чата:
/// позволяет запустить процесс, проверять ожидание ввода имени и сбрасывать состояние.
/// </summary>
public interface IAddClientStateStore
{
    /// <summary>
    /// Инициализирует сценарий добавления клиента для указанного чата
    /// (переводит чат в состояние ожидания ввода имени).
    /// </summary>
    /// <param name="chatId">Идентификатор чата Telegram, для которого запускается сценарий.</param>
    public void Begin(long chatId);

    /// <summary>
    /// Проверяет, находится ли указанный чат в состоянии ожидания ввода имени нового клиента.
    /// </summary>
    /// <param name="chatId">Идентификатор чата Telegram.</param>
    /// <returns><c>true</c>, если ожидается ввод имени; иначе <c>false</c>.</returns>
    public bool IsWaitingName(long chatId);

    /// <summary>
    /// Сбрасывает состояние сценария добавления клиента для указанного чата
    /// (отменяет ожидание ввода и очищает сохранённые данные, если они есть).
    /// </summary>
    /// <param name="chatId">Идентификатор чата Telegram, состояние которого нужно очистить.</param>
    public void Clear(long chatId);
}