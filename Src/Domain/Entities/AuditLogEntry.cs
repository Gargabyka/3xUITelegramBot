using Domain.Enums;
using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Запись журнала действий: кто, что и когда изменил через бота
/// </summary>
public class AuditLogEntry : IEntity
{
	/// <summary>
	/// Идентификатор
	/// </summary>
	public int Id { get; init; }

	/// <summary>
	/// Telegram ID того, кто выполнил действие
	/// </summary>
	public long ActorTelegramId { get; init; }

	/// <summary>
	/// Имя выполнившего на момент действия (пользователя могут удалить, а запись должна остаться читаемой)
	/// </summary>
	public string? ActorName { get; init; }

	/// <summary>
	/// Действие
	/// </summary>
	public AuditAction Action { get; init; }

	/// <summary>
	/// Подробности: над чем выполнено действие
	/// </summary>
	public required string Details { get; init; }

	/// <summary>
	/// Когда (UTC)
	/// </summary>
	public DateTime CreatedAtUtc { get; init; }
}
