using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Telegram.Store;

/// <summary>
/// Состояние многошагового сценария по чатам (бот ждёт от пользователя ввод).
/// Состояние живёт ограниченное время: забытый сценарий не должен перехватить сообщение, отправленное через час
/// </summary>
/// <typeparam name="TState">Данные сценария</typeparam>
public sealed class ExpiringStateStore<TState>
	where TState : class
{
	private readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);

	private readonly ConcurrentDictionary<long, (TState State, DateTime ExpiresAtUtc)> _states = new();

	public void Set(long chatId, TState state) => _states[chatId] = (state, DateTime.UtcNow + _lifetime);

	public bool TryGet(long chatId, [MaybeNullWhen(false)] out TState state)
	{
		state = null;
		if (!_states.TryGetValue(chatId, out var entry))
		{
			return false;
		}

		if (entry.ExpiresAtUtc <= DateTime.UtcNow)
		{
			_states.TryRemove(chatId, out _);
			return false;
		}

		state = entry.State;
		return true;
	}

	public void Clear(long chatId) => _states.TryRemove(chatId, out _);
}