using System.Collections.Concurrent;
using Telegram.Store.Interfaces;

namespace Telegram.Store;

public sealed class InMemoryAddUserStateStore : IAddUserStateStore
{
	// Забытый сценарий не должен перехватить контакт, отправленный через час
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

	private readonly ConcurrentDictionary<long, DateTime> _waiting = new();

	public void Begin(long chatId) => _waiting[chatId] = DateTime.UtcNow + Lifetime;

	public bool IsWaitingContact(long chatId)
	{
		if (!_waiting.TryGetValue(chatId, out var expiresAtUtc))
		{
			return false;
		}

		if (expiresAtUtc <= DateTime.UtcNow)
		{
			_waiting.TryRemove(chatId, out _);
			return false;
		}

		return true;
	}

	public void Clear(long chatId) => _waiting.TryRemove(chatId, out _);
}
