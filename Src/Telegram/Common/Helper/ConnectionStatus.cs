using Domain.Enums;
using Domain.Extensions;
using Domain.Models;

namespace Telegram.Common.Helper;

/// <summary>
/// Статус соединения в списках. Статус пишется в тексте сообщения, а не на кнопках:
/// текст кнопок Telegram выравнивает по центру, и значки не встают в колонку
/// </summary>
public static class ConnectionStatus
{
	/// <summary>
	/// Расшифровка значков под списком
	/// </summary>
	public const string Legend = "🟢 онлайн · ⚪ не в сети · 🔴 отключено";

	/// <summary>
	/// Сколько назад была активность, чтобы считать клиента онлайн.
	/// Панели обновляют время активности раз в несколько секунд, рукопожатие WireGuard — раз в 2 минуты
	/// </summary>
	public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(3);

	public static bool IsOnline(VpnServerClient client)
		=> client.LatestHandshakeAt >= DateTimeOffset.UtcNow - OnlineWindow;

	/// <summary>
	/// Значок статуса; клиент null — сервер не ответил или клиента на нём нет
	/// </summary>
	public static string Icon(VpnServerClient? client) => client switch
	{
		null => "❔",
		{ Enabled: false } => "🔴",
		_ when IsOnline(client) => "🟢",
		_ => "⚪",
	};

	/// <summary>
	/// Строка списка: значок, имя и короткая метка профиля
	/// </summary>
	public static string Line(string name, VpnProfileType profile, VpnServerClient? client)
		=> $"{Icon(client)} {name} · {profile.ShortTitle()}";

	/// <summary>
	/// Подпись кнопки: имя первым, чтобы при нехватке места обрезалась метка, а не название
	/// </summary>
	public static string ButtonText(string name, VpnProfileType profile)
		=> $"{name} · {profile.ShortTitle()}";
}
