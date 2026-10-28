namespace Domain.Models;

/// <summary>
/// Состояние VPN-сервера одного протокола
/// </summary>
/// <param name="Profile">Профиль подключения сервера</param>
/// <param name="Clients">Клиенты сервера (пусто, если сервер недоступен)</param>
/// <param name="Error">Причина недоступности (null — сервер доступен)</param>
public record VpnBackendStatus(VpnProfile Profile, VpnServerClient[] Clients, string? Error)
{
	/// <summary>
	/// Сервер ответил на запрос
	/// </summary>
	public bool IsAvailable => Error is null;
}