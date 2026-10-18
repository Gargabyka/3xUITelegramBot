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
}
