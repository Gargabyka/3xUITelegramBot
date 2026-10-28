using Domain.Enums;
using Domain.Extensions;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Объединяет все VPN-серверы и направляет операции на нужный
/// </summary>
public sealed class VpnApiService : IVpnApiService
{
	private readonly IReadOnlyList<IVpnBackend> _backends;
	private readonly ILogger<VpnApiService> _logger;

	public VpnApiService(IEnumerable<IVpnBackend> backends, ILogger<VpnApiService> logger)
	{
		_backends = backends.ToArray();
		_logger = logger;
	}

	public IReadOnlyList<VpnProfile> AvailableProfiles => _backends.Select(x => x.Profile).ToArray();

	public async Task<VpnBackendStatus[]> GetStatusAsync(CancellationToken cancellationToken)
	{
		var result = new List<VpnBackendStatus>();
		foreach (var backend in _backends)
		{
			try
			{
				result.Add(new VpnBackendStatus(backend.Profile, await backend.GetClients(cancellationToken), null));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Недоступность одного сервера не должна ломать работу с остальными
				_logger.LogWarning(ex, "Не удалось получить клиентов профиля {Profile}", backend.Profile.Type);
				result.Add(new VpnBackendStatus(backend.Profile, [], ex.Message));
			}
		}

		return result.ToArray();
	}

	public async Task<VpnServerClient[]> GetClients(CancellationToken cancellationToken)
		=> (await GetStatusAsync(cancellationToken))
			.SelectMany(x => x.Clients)
			.GroupBy(x => x.Id)
			.Select(x => x.First()) // клиент, привязанный к нескольким inbound'ам, считается один раз
			.ToArray();

	public async Task<byte[]> DownloadConfigAsync(Guid id, CancellationToken cancellationToken)
		=> await (await ResolveAsync(id, cancellationToken)).DownloadConfigAsync(id, cancellationToken);

	public async Task DeleteClientAsync(Guid id, CancellationToken cancellationToken)
		=> await (await ResolveAsync(id, cancellationToken)).DeleteClientAsync(id, cancellationToken);

	public Task AddClientAsync(VpnProfileType profile, string name, CancellationToken cancellationToken)
		=> GetBackend(profile).AddClientAsync(name, cancellationToken);

	public async Task EnableClientAsync(Guid id, CancellationToken cancellationToken)
		=> await (await ResolveAsync(id, cancellationToken)).EnableClientAsync(id, cancellationToken);

	public async Task DisableClientAsync(Guid id, CancellationToken cancellationToken)
		=> await (await ResolveAsync(id, cancellationToken)).DisableClientAsync(id, cancellationToken);

	public async Task RenameClientAsync(Guid id, string newName, CancellationToken cancellationToken)
		=> await (await ResolveAsync(id, cancellationToken)).RenameClientAsync(id, newName, cancellationToken);

	private IVpnBackend GetBackend(VpnProfileType profile)
		=> _backends.FirstOrDefault(x => x.Profile.Type == profile)
		   ?? throw new InvalidOperationException($"Профиль подключения {profile.Title()} не настроен");

	private async Task<IVpnBackend> ResolveAsync(Guid id, CancellationToken cancellationToken)
	{
		foreach (var backend in _backends)
		{
			var clients = await backend.GetClients(cancellationToken);
			if (clients.Any(x => x.Id == id))
			{
				return backend;
			}
		}

		throw new InvalidOperationException($"Клиент {id} не найден ни на одном сервере");
	}
}
