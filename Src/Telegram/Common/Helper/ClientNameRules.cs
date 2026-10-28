using System.Text.RegularExpressions;

namespace Telegram.Common.Helper;

/// <summary>
/// Правила имени соединения (при создании и переименовании)
/// </summary>
public static class ClientNameRules
{
	/// <summary>
	/// Подсказка для пользователя
	/// </summary>
	public const string Hint = "Имя должно быть от 3 до 32 символов: латинские буквы, цифры, «_», «.» или «-».";

	// Имя уходит в URL API 3x-ui и в статистику Xray (email клиента) — только латиница, цифры и _ . -
	private static readonly Regex NameRegex = new("^[A-Za-z0-9_.-]{3,32}$", RegexOptions.Compiled);

	public static bool IsValid(string name) => NameRegex.IsMatch(name);
}
