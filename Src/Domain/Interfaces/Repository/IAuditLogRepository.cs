using Domain.Entities;

namespace Domain.Interfaces.Repository;

/// <summary>
/// Интерфейс для работы с <see cref="AuditLogEntry"/>
/// </summary>
public interface IAuditLogRepository
{
	/// <summary>
	/// Добавить запись в контекст (для последующего SaveChanges)
	/// </summary>
	/// <param name="entry">Запись</param>
	public void Add(AuditLogEntry entry);

	/// <summary>
	/// Количество записей
	/// </summary>
	/// <param name="cancellationToken">Токен отмены</param>
	public Task<int> CountAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Страница записей, новые первыми
	/// </summary>
	/// <param name="skip">Сколько пропустить</param>
	/// <param name="take">Сколько взять</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Записи журнала</returns>
	/// <remarks>Недоступен для редактирования (AsNoTracking)</remarks>
	public Task<AuditLogEntry[]> GetPageAsync(int skip, int take, CancellationToken cancellationToken);
}