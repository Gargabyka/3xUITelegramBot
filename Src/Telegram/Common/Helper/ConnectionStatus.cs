using Domain.Enums;
using Domain.Extensions;
using Domain.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace Telegram.Common.Helper;

/// <summary>
/// Статус соединения в списках: значок на кнопке соединения, кнопки — сеткой 3×3
/// </summary>
public static class ConnectionStatus
{
	/// <summary>
	/// Кнопок в ряду: на странице 9 соединений — сетка 3×3
	/// </summary>
	public const int Columns = 3;

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
	/// Подпись кнопки: значок, затем имя — при нехватке места обрезается короткая метка профиля, а не название
	/// </summary>
	public static string ButtonText(string name, VpnProfileType profile, VpnServerClient? client)
		=> $"{Icon(client)} {name} · {profile.ShortTitle()}";

	/// <summary>
	/// Разложить кнопки сеткой по <see cref="Columns"/> в ряд
	/// </summary>
	public static List<InlineKeyboardButton[]> ToGrid(IEnumerable<InlineKeyboardButton> buttons)
		=> buttons.Chunk(Columns).ToList();
}
