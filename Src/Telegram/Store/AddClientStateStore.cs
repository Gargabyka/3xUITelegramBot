using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Domain.Models;
using Telegram.Store.Interfaces;

namespace Telegram.Store;

public sealed class InMemoryAddClientStateStore : IAddClientStateStore
{
    private readonly ConcurrentDictionary<long, VpnProfile?> _waiting = new();

    public void Begin(long chatId, VpnProfile profile) => _waiting[chatId] = profile;

    public bool IsWaitingName(long chatId) => _waiting.ContainsKey(chatId);

    public bool TryGetProfile(long chatId, [MaybeNullWhen(false)] out VpnProfile profile) => _waiting.TryGetValue(chatId, out profile);

    public void Clear(long chatId) => _waiting.TryRemove(chatId, out _);
}