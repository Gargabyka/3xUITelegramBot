using Domain.Enums;

namespace Domain.Extensions;

public static class VpnProfileTypeExtensions
{
	/// <summary>
	/// Название профиля для пользователя
	/// </summary>
	public static string Title(this VpnProfileType profile) => profile switch
	{
		VpnProfileType.VlessRaw => "VLESS Raw",
		VpnProfileType.VlessXhttp => "VLESS XHTTP",
		VpnProfileType.VlessXhttpCdn => "VLESS XHTTP CDN",
		VpnProfileType.AmneziaWg => "AmneziaWG",
		_ => profile.ToString(),
	};

	/// <summary>
	/// Подсказка по выбору профиля
	/// </summary>
	public static string Description(this VpnProfileType profile) => profile switch
	{
		VpnProfileType.VlessRaw => "основной вариант: быстрый и простой. Начните с него.",
		VpnProfileType.VlessXhttp => "если Raw не подключается или работает нестабильно: другой способ передачи, лучше маскируется.",
		VpnProfileType.VlessXhttpCdn => "запасной вариант, если блокируют по IP: трафик идёт через CDN. Скорость может быть ниже.",
		VpnProfileType.AmneziaWg => "VPN на WireGuard с защитой от блокировок. Работает на уровне всей системы, нужно приложение AmneziaVPN.",
		_ => string.Empty,
	};

	/// <summary>
	/// Профиль работает через 3x-ui (VLESS)
	/// </summary>
	public static bool IsVless(this VpnProfileType profile) => profile != VpnProfileType.AmneziaWg;

	/// <summary>
	/// Имя файла конфигурации при отправке пользователю
	/// </summary>
	public static string ConfigFileName(this VpnProfileType profile) => profile.IsVless() ? "vless.txt" : "amnezia.conf";

	/// <summary>
	/// Что содержит конфигурация
	/// </summary>
	public static string ConfigKind(this VpnProfileType profile) => profile.IsVless() ? "Ссылка VLESS" : "Конфигурация AmneziaWG";
}
