namespace AmneziaWg.Models;

/// <summary>
/// Клиент в ответе API amnezia-wg-easy
/// </summary>
internal sealed record AmneziaWgClient(
	Guid Id,
	string Name,
	bool Enabled,
	string Address,
	string PublicKey,
	DateTimeOffset CreatedAt,
	DateTimeOffset? LatestHandshakeAt,
	long TransferRx,
	long TransferTx);
