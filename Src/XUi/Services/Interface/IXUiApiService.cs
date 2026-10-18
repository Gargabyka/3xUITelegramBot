using XUi.Models;

namespace XUi.Services.Interface;

/// <summary>
/// Интерфейс для работы с 3x-ui API
/// </summary>
public interface IXUiApiService
{
    /// <summary>
    /// Получить клиентов 3x-ui
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Возвращает клиентов</returns>
    public Task<XUiClient[]?> GetClients(CancellationToken cancellationToken);

    /// <summary>
    /// Получить ссылку подключения клиента
    /// </summary>
    /// <param name="id">Идентификатор клиента</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив байтов конфигурации</returns>
    public Task<byte[]> DownloadConfigAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Удалить клиента 3x-ui
    /// </summary>
    /// <param name="id">Идентификатор клиента</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task DeleteClientAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Добавить клиента 3x-ui
    /// </summary>
    /// <param name="name">Имя клиента</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task AddClientAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Включить клиента 3x-ui
    /// </summary>
    /// <param name="id">Идентификатор клиента</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task EnableClientAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Отключить клиента 3x-ui
    /// </summary>
    /// <param name="id">Идентификатор клиента</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task DisableClientAsync(Guid id, CancellationToken cancellationToken);
}