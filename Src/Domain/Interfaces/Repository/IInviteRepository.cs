using Domain.Entities;

namespace Domain.Interfaces.Repository;

/// <summary>
/// Интерфейс для работы с <see cref="Invite"/>
/// </summary>
public interface IInviteRepository
{
	/// <summary>
	/// Добавить приглашение в контекст (для последующего SaveChanges)
	/// </summary>
	/// <param name="invite">Приглашение</param>
	public void Add(Invite invite);

	/// <summary>
	/// Атомарно занять приглашение: срабатывает только для неиспользованного и непросроченного
	/// </summary>
	/// <param name="token">Токен из ссылки</param>
	/// <param name="nowUtc">Текущее время (UTC)</param>
	/// <param name="telegramId">Telegram ID принявшего приглашение</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Приглашение, если удалось занять; иначе null</returns>
	public Task<Invite?> TryClaimAsync(string token, DateTime nowUtc, long telegramId, CancellationToken cancellationToken);
}
