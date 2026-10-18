using Domain.Interfaces;
using Domain.Models;

namespace Domain.Entities;

/// <summary>
/// Соединения VPN
/// </summary>
public class VpnPeer : IEntity
{
	/// <summary>
	/// Идентификатор
	/// </summary>
	public int Id { get; init; }

	/// <summary>
	/// Идентификатор клиента в 3x-ui (UUID VLESS)
	/// </summary>
	public Guid ClientId { get; set; }

	/// <summary>
	/// Имя клиента
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Признак включения VPN
	/// </summary>
	public bool IsEnabled { get; set; }

	/// <summary>
	/// Адрес
	/// </summary>
	public	string Address { get; set; }

	/// <summary>
	/// Публичный ключ
	/// </summary>
	public string PublicKey { get; set; }

	/// <summary>
	/// Дата создания
	/// </summary>
	public DateOnly CreatedAt { get; set; }

	public void Update(VpnClient client)
	{
		Name = client.Name;
		Address = client.Address;
		CreatedAt = client.CreatedAt;
		IsEnabled = client.Enabled;
		PublicKey = client.PublicKey;
	}
}