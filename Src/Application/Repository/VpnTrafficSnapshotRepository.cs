using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class VpnTrafficSnapshotRepository : IVpnTrafficSnapshotRepository
{
	private readonly AppDbContext _dbContext;

	public VpnTrafficSnapshotRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void AddRange(VpnTrafficSnapshot[] snapshots)
		=> _dbContext.VpnTrafficSnapshots.AddRange(snapshots);

	public async Task<VpnTrafficSnapshot[]> GetSnapshotsForPeriodAsync(
		Guid clientId,
		DateTime? sinceUtc,
		CancellationToken cancellationToken)
	{
		var query = _dbContext.VpnTrafficSnapshots
			.AsNoTracking()
			.Where(x => x.ClientId == clientId);

		if (!sinceUtc.HasValue)
		{
			return await query
				.OrderBy(x => x.CapturedAtUtc)
				.ToArrayAsync(cancellationToken);
		}

		// Последний снимок ДО начала периода — база для расчёта расхода в первый же час периода.
		var baseline = await query
			.Where(x => x.CapturedAtUtc < sinceUtc.Value)
			.OrderByDescending(x => x.CapturedAtUtc)
			.FirstOrDefaultAsync(cancellationToken);

		var periodSnapshots = await query
			.Where(x => x.CapturedAtUtc >= sinceUtc.Value)
			.OrderBy(x => x.CapturedAtUtc)
			.ToArrayAsync(cancellationToken);

		return baseline is null
			? periodSnapshots
			: [baseline, .. periodSnapshots];
	}

	public Task<DateTime?> GetFirstSnapshotDateAsync(Guid clientId, CancellationToken cancellationToken)
		=> _dbContext.VpnTrafficSnapshots
			.AsNoTracking()
			.Where(x => x.ClientId == clientId)
			.OrderBy(x => x.CapturedAtUtc)
			.Select(x => (DateTime?)x.CapturedAtUtc)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<int> CompactAsync(DateTime olderThanUtc, CancellationToken cancellationToken)
	{
		var clientIds = await _dbContext.VpnTrafficSnapshots
			.AsNoTracking()
			.Where(x => x.CapturedAtUtc < olderThanUtc)
			.Select(x => x.ClientId)
			.Distinct()
			.ToArrayAsync(cancellationToken);

		var removed = 0;
		foreach (var clientId in clientIds)
		{
			var old = await _dbContext.VpnTrafficSnapshots
				.AsNoTracking()
				.Where(x => x.ClientId == clientId && x.CapturedAtUtc < olderThanUtc)
				.OrderBy(x => x.CapturedAtUtc)
				.ToArrayAsync(cancellationToken);

			// Первый снимок после границы — правый сосед последнего старого
			var next = await _dbContext.VpnTrafficSnapshots
				.AsNoTracking()
				.Where(x => x.ClientId == clientId && x.CapturedAtUtc >= olderThanUtc)
				.OrderBy(x => x.CapturedAtUtc)
				.FirstOrDefaultAsync(cancellationToken);

			var idsToDelete = new List<int>();
			for (var i = 1; i < old.Length; i++)
			{
				var right = i + 1 < old.Length ? old[i + 1] : next;
				if (right is not null && IsMonotonic(old[i - 1], old[i]) && IsMonotonic(old[i], right))
				{
					idsToDelete.Add(old[i].Id);
				}
			}

			foreach (var chunk in idsToDelete.Chunk(500))
			{
				removed += await _dbContext.VpnTrafficSnapshots
					.Where(x => chunk.Contains(x.Id))
					.ExecuteDeleteAsync(cancellationToken);
			}
		}

		return removed;
	}

	private static bool IsMonotonic(VpnTrafficSnapshot prev, VpnTrafficSnapshot cur)
		=> cur.TransferRx >= prev.TransferRx && cur.TransferTx >= prev.TransferTx;
}