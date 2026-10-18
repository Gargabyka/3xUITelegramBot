using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class VpnUserRepository : IVpnUserRepository
{
	private readonly AppDbContext _dbContext;

	public VpnUserRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task<VpnUser[]> GetConnectionsByTelegramIdAsync(int telegramUserId, CancellationToken cancellationToken)
		=> _dbContext
			.VpnUsers
			.AsNoTracking()
			.Include(x => x.Peer)
			.Where(x => x.TelegramUserId == telegramUserId)
			.ToArrayAsync(cancellationToken);

	public void Add(VpnUser vpnUser)
		=> _dbContext.VpnUsers.Add(vpnUser);

	public Task<bool> ExistsAsync(int telegramUserId, int peerId, CancellationToken cancellationToken)
		=> _dbContext
			.VpnUsers
			.AsNoTracking()
			.AnyAsync(x => x.TelegramUserId == telegramUserId && x.PeerId == peerId, cancellationToken);

	public Task<int[]> GetUsedPeerIdsAsync(CancellationToken cancellationToken)
		=> _dbContext
			.VpnUsers
			.AsNoTracking()
			.Select(x => x.PeerId)
			.Distinct()
			.ToArrayAsync(cancellationToken);

	public Task<int> RemoveLinkAsync(int telegramUserId, int peerId, CancellationToken cancellationToken)
		=> _dbContext
			.VpnUsers
			.Where(x => x.TelegramUserId == telegramUserId && x.PeerId == peerId)
			.ExecuteDeleteAsync(cancellationToken);
}