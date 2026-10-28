using System.ComponentModel.DataAnnotations;

namespace Domain.Enums;

/// <summary>
/// Действие, записываемое в журнал
/// </summary>
public enum AuditAction
{
	[Display(Description = "Создано соединение")]
	ClientCreated = 1,

	[Display(Description = "Удалено соединение")]
	ClientDeleted = 2,

	[Display(Description = "Соединение включено")]
	ClientEnabled = 3,

	[Display(Description = "Соединение отключено")]
	ClientDisabled = 4,

	[Display(Description = "Соединение переименовано")]
	ClientRenamed = 5,

	[Display(Description = "Добавлен пользователь")]
	UserAdded = 10,

	[Display(Description = "Удалён пользователь")]
	UserDeleted = 11,

	[Display(Description = "Изменена роль")]
	UserRoleChanged = 12,

	[Display(Description = "Изменён лимит соединений")]
	UserLimitChanged = 13,

	[Display(Description = "Изменены напоминания об оплате")]
	UserReminderToggled = 14,

	[Display(Description = "Соединение привязано к пользователю")]
	ConnectionLinked = 15,

	[Display(Description = "Соединение отвязано от пользователя")]
	ConnectionUnlinked = 16,

	[Display(Description = "Создано приглашение")]
	InviteCreated = 20,

	[Display(Description = "Принято приглашение")]
	InviteAccepted = 21,

	[Display(Description = "Синхронизация соединений")]
	ConnectionsSynced = 30,

	[Display(Description = "Рассылка")]
	Broadcast = 31,
}
