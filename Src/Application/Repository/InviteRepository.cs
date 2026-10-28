using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class InviteRepository : IInviteRepository
{
	private readonly AppDbContext _dbContext;

	public InviteRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(Invite invite) => _dbContext.Invites.Add(invite);

	public async Task<Invite?> TryClaimAsync(string token, DateTime nowUtc, long telegramId, CancellationToken cancellationToken)
	{
		// Один UPDATE с условием: два одновременных перехода по ссылке не пройдут оба
		var claimed = await _dbContext.Invites
			.Where(x => x.Token == token && x.UsedAtUtc == null && x.ExpiresAtUtc > nowUtc)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(x => x.UsedAtUtc, nowUtc)
					.SetProperty(x => x.UsedByTelegramId, telegramId),
				cancellationToken);

		return claimed == 0
			? null
			: await _dbContext.Invites.AsNoTracking().FirstAsync(x => x.Token == token, cancellationToken);
	}
}
