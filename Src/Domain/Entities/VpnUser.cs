using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Связь пользователя и peer
/// </summary>
public class VpnUser : IEntity
{
	/// <summary>
	/// Идентификатор
	/// </summary>
	public int Id { get; init; }

	/// <summary>
	/// Идентификатор Telegram
	/// </summary>
	public int TelegramUserId { get; set; }

	/// <summary>
	/// Идентификатор соединения (VpnPeer)
	/// </summary>
	public int PeerId { get; set; }

	public virtual TelegramUser Telegram { get; set; }

	public virtual VpnPeer Peer { get; set; }
}