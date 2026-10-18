using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class VpnPeerRepository : IVpnPeerRepository
{
	private readonly AppDbContext _dbContext;

	public VpnPeerRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(VpnPeer peer)
		=> _dbContext.VpnPeers.Add(peer);

	public void AddRange(VpnPeer[] peers)
		=> _dbContext.VpnPeers.AddRange(peers);

	public void Remove(VpnPeer peer)
		=> _dbContext.VpnPeers.Remove(peer);

	public Task<VpnPeer[]> GetPeersAsync(CancellationToken cancellationToken)
		=> _dbContext
			.VpnPeers
			.AsNoTracking()
			.ToArrayAsync(cancellationToken);

	public Task<VpnPeer?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken)
		=> _dbContext
			.VpnPeers
			.FirstOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);
}