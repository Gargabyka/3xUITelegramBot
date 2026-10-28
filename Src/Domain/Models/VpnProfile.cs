using Domain.Enums;
using Domain.Extensions;

namespace Domain.Models;

/// <summary>
/// Вариант подключения, который можно выбрать при создании соединения
/// </summary>
/// <param name="Type">Тип профиля</param>
public record VpnProfile(VpnProfileType Type)
{
	/// <summary>
	/// Название для пользователя
	/// </summary>
	public string Title => Type.Title();
}
