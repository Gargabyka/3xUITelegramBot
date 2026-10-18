using System.Collections.Concurrent;
using Telegram.Store.Interfaces;

namespace Telegram.Store;

public sealed class InMemoryAddUserStateStore : IAddUserStateStore
{
	private readonly ConcurrentDictionary<long, byte> _waiting = new();

	public void Begin(long chatId) => _waiting[chatId] = 1;

	public bool IsWaitingContact(long chatId) => _waiting.ContainsKey(chatId);

	public void Clear(long chatId) => _waiting.TryRemove(chatId, out _);
}