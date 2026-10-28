using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Telegram.Service;

/// <summary>
/// Запись в журнал действий из обработчиков. Журнал вспомогательный: его сбой
/// не должен ломать уже выполненное действие, поэтому ошибки только пишутся в лог
/// </summary>
public sealed class AuditLogger
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<AuditLogger> _logger;

	public AuditLogger(IServiceScopeFactory scopeFactory, ILogger<AuditLogger> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	/// <summary>
	/// Пользователь для подробностей записи: имя, @username и Telegram ID
	/// </summary>
	public static string Describe(TelegramUser user)
	{
		var name = $"{user.FirstName} {user.LastName}".Trim();
		if (!string.IsNullOrWhiteSpace(user.UserName))
		{
			name = $"{name} @{user.UserName}".Trim();
		}

		return $"{name} [{user.TelegramId}]".Trim();
	}

	public async Task LogAsync(long actorTelegramId, AuditAction action, string details, CancellationToken cancellationToken)
	{
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var service = scope.ServiceProvider.GetRequiredService<AuditLogService>();
			await service.WriteAsync(actorTelegramId, action, details, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Не удалось записать в журнал действие {Action}", action);
		}
	}
}
