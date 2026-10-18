using Domain.Entities;

namespace Domain.Interfaces.Repository;

/// <summary>
/// Интерфейс для работы с <see cref="HostingReminderState"/>
/// </summary>
public interface IHostingReminderStateRepository
{
	/// <summary>
	/// Получить состояние рассылки напоминаний (создать, если ещё нет)
	/// </summary>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Возвращает состояние, доступное для редактирования</returns>
	public Task<HostingReminderState> GetOrCreateAsync(CancellationToken cancellationToken);
}
