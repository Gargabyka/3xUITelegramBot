using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Interfaces.Repository;

namespace Application.Services;

/// <summary>
/// Запись действий в журнал
/// </summary>
public sealed class AuditLogService
{
	private const int MaxDetailsLength = 1024;

	private readonly IAuditLogRepository _auditLogRepository;
	private readonly ITelegramUserRepository _telegramUserRepository;
	private readonly IUnitOfWork _unitOfWork;

	public AuditLogService(
		IAuditLogRepository auditLogRepository,
		ITelegramUserRepository telegramUserRepository,
		IUnitOfWork unitOfWork)
	{
		_auditLogRepository = auditLogRepository;
		_telegramUserRepository = telegramUserRepository;
		_unitOfWork = unitOfWork;
	}

	/// <summary>
	/// Записать действие
	/// </summary>
	/// <param name="actorTelegramId">Telegram ID выполнившего</param>
	/// <param name="action">Действие</param>
	/// <param name="details">Подробности</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Задача записи</returns>
	public async Task WriteAsync(long actorTelegramId, AuditAction action, string details, CancellationToken cancellationToken)
	{
		var actor = await _telegramUserRepository.GetUserByTelegramIdAsync(actorTelegramId, cancellationToken);

		_auditLogRepository.Add(new AuditLogEntry
		{
			ActorTelegramId = actorTelegramId,
			ActorName = actor is null ? null : FormatName(actor),
			Action = action,
			Details = details.Length > MaxDetailsLength ? details[..MaxDetailsLength] : details,
			CreatedAtUtc = DateTime.UtcNow,
		});

		await _unitOfWork.SaveChangesAsync(cancellationToken);
	}

	private static string FormatName(TelegramUser user)
	{
		var name = $"{user.FirstName} {user.LastName}".Trim();
		return string.IsNullOrWhiteSpace(user.UserName) ? name : $"{name} (@{user.UserName})".Trim();
	}
}
