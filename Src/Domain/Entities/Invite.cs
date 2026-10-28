using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Одноразовое приглашение в бот (ссылка t.me/бот?start=inv_токен)
/// </summary>
public class Invite : IEntity
{
	/// <summary>
	/// Идентификатор
	/// </summary>
	public int Id { get; init; }

	/// <summary>
	/// Секретный токен из ссылки
	/// </summary>
	public required string Token { get; init; }

	/// <summary>
	/// Telegram ID администратора, создавшего приглашение
	/// </summary>
	public long CreatedByTelegramId { get; init; }

	/// <summary>
	/// Когда создано (UTC)
	/// </summary>
	public DateTime CreatedAtUtc { get; init; }

	/// <summary>
	/// До какого момента ссылка действует (UTC)
	/// </summary>
	public DateTime ExpiresAtUtc { get; init; }

	/// <summary>
	/// Когда использовано (UTC), null — ещё не использовано
	/// </summary>
	public DateTime? UsedAtUtc { get; set; }

	/// <summary>
	/// Telegram ID пользователя, принявшего приглашение
	/// </summary>
	public long? UsedByTelegramId { get; set; }
}
