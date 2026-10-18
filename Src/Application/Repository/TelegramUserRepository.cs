using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class TelegramUserRepository : ITelegramUserRepository
{
	private readonly AppDbContext _dbContext;

	public TelegramUserRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task<TelegramUser[]> GetUsersAsync(CancellationToken cancellationToken)
		=> _dbContext
			.TelegramUsers
			.AsNoTracking()
			.ToArrayAsync(cancellationToken);

	public Task<TelegramUser?> GetUserByTelegramIdAsync(long telegramId, CancellationToken cancellationToken)
		=> _dbContext
			.TelegramUsers
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.TelegramId == telegramId, cancellationToken);

	public Task<TelegramUser?> GetUserByTelegramIdForUpdateAsync(long telegramId, CancellationToken cancellationToken)
		=> _dbContext
			.TelegramUsers
			.FirstOrDefaultAsync(x => x.TelegramId == telegramId, cancellationToken);

	public Task<TelegramUser[]> GetUsersForReminderAsync(CancellationToken cancellationToken)
		=> _dbContext
			.TelegramUsers
			.AsNoTracking()
			.Where(x => !x.ReminderOptOut)
			.ToArrayAsync(cancellationToken);

	public void Add(TelegramUser user)
		=> _dbContext.TelegramUsers.Add(user);

	public void Remove(TelegramUser user)
		=> _dbContext.TelegramUsers.Remove(user);
}