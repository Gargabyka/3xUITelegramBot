using System.ComponentModel.DataAnnotations;

namespace Domain.Enums;

/// <summary>
/// Роль пользователя
/// </summary>
public enum UserRole
{
	/// <summary>
	/// Неизвестный
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Администратор
	/// </summary>
	[Display(Description = "Администратор")]
	Administrator = 1,

	/// <summary>
	/// Пользователь
	/// </summary>
	[Display(Description = "Пользователь")]
	User = 2,
}