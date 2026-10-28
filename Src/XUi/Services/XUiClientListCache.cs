using System.Diagnostics.CodeAnalysis;
using XUi.Models;

namespace XUi.Services;

/// <summary>
/// Общий для всех inbound'ов кэш списка клиентов 3x-ui.
/// Панель отдаёт клиентов всех inbound'ов одним запросом, а сервисов XUiApiService несколько (по одному на inbound),
/// и одно действие в боте (карточка, включение, удаление) запрашивает список по нескольку раз.
/// Кэш живёт несколько секунд и сбрасывается после любого изменения клиентов.
/// </summary>
public sealed class XUiClientListCache : IDisposable
{
	private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

	private readonly SemaphoreSlim _lock = new(1, 1);
	private XUiPanelSnapshot? _snapshot;
	private long _version;

	/// <summary>
	/// Список клиентов панели и время их последнего подключения
	/// </summary>
	public async Task<XUiPanelSnapshot> GetAsync(Func<CancellationToken, Task<XUiPanelSnapshot>> fetch, CancellationToken cancellationToken)
	{
		if (TryGetFresh(out var cached))
		{
			return cached;
		}

		await _lock.WaitAsync(cancellationToken);
		try
		{
			if (TryGetFresh(out cached))
			{
				return cached;
			}

			var version = Interlocked.Read(ref _version);
			var snapshot = await fetch(cancellationToken);

			// Если список успели изменить, пока шёл запрос, — не кэшируем возможно устаревший ответ
			if (Interlocked.Read(ref _version) == version)
			{
				_snapshot = snapshot;
			}

			return snapshot;
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Сбросить кэш (после добавления, удаления, включения или отключения клиента)
	/// </summary>
	public void Invalidate()
	{
		Interlocked.Increment(ref _version);
		_snapshot = null;
	}

	public void Dispose() => _lock.Dispose();

	private bool TryGetFresh([NotNullWhen(true)] out XUiPanelSnapshot? snapshot)
	{
		// Кэш пуст до первого запроса и после каждого Invalidate()
		snapshot = _snapshot;
		return snapshot is not null && DateTime.UtcNow - snapshot.FetchedAtUtc < Ttl;
	}
}