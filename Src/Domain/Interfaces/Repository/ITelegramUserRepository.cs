using Domain.Entities;

namespace Domain.Interfaces.Repository;

/// <summary>
/// Интерфейс для работы с <see cref="TelegramUser"/>
/// </summary>
public interface ITelegramUserRepository
{
	/// <summary>
	/// Получить пользователей
	/// </summary>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Возвращает пользователей</returns>
	/// <remarks>Недоступен для редактирования (AsNoTracking)</remarks>
	public Task<TelegramUser[]> GetUsersAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Получить пользователя по TelegramId
	/// </summary>
	/// <param name="telegramId">Идентификатор телеграмма</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Возвращает пользователя</returns>
	/// <remarks>Недоступен для редактирования (AsNoTracking)</remarks>
	public Task<TelegramUser?> GetUserByTelegramIdAsync(long telegramId, CancellationToken cancellationToken);

	/// <summary>
	/// Получить пользователя по TelegramId
	/// </summary>
	/// <param name="telegramId">Идентификатор телеграмма</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Возвращает пользователя</returns>
	public Task<TelegramUser?> GetUserByTelegramIdForUpdateAsync(long telegramId, CancellationToken cancellationToken);

	/// <summary>
	/// Получить пользователей, которым можно присылать напоминания об оплате хостинга
	/// </summary>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Возвращает пользователей, не отключивших напоминания</returns>
	/// <remarks>Недоступен для редактирования (AsNoTracking)</remarks>
	public Task<TelegramUser[]> GetUsersForReminderAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Добавить пользователя в контекст (для последующего SaveChanges)
	/// </summary>
	/// <param name="user">Пользователь</param>
	public void Add(TelegramUser user);

	/// <summary>
	/// Удалить пользователя.
	/// </summary>
	public void Remove(TelegramUser user);
}