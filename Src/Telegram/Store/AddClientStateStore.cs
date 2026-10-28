using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Domain.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Store;

public sealed class InMemoryAddClientStateStore : IAddClientStateStore
{
    // Забытый сценарий не должен перехватить сообщение, отправленное через час
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<long, (VpnProfile Profile, DateTime ExpiresAtUtc)> _waiting = new();

    public void Begin(long chatId, VpnProfile profile) => _waiting[chatId] = (profile, DateTime.UtcNow + Lifetime);

    public bool IsWaitingName(long chatId) => TryGetProfile(chatId, out _);

    public bool TryGetProfile(long chatId, [MaybeNullWhen(false)] out VpnProfile profile)
    {
        profile = null;
        if (!_waiting.TryGetValue(chatId, out var state))
        {
            return false;
        }

        if (state.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _waiting.TryRemove(chatId, out _);
            return false;
        }

        profile = state.Profile;
        return true;
    }

    public void Clear(long chatId) => _waiting.TryRemove(chatId, out _);
}
