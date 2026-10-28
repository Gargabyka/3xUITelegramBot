using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class AuditLogRepository : IAuditLogRepository
{
	private readonly AppDbContext _dbContext;

	public AuditLogRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public void Add(AuditLogEntry entry) => _dbContext.AuditLog.Add(entry);

	public Task<int> CountAsync(CancellationToken cancellationToken)
		=> _dbContext.AuditLog.CountAsync(cancellationToken);

	public Task<AuditLogEntry[]> GetPageAsync(int skip, int take, CancellationToken cancellationToken)
		=> _dbContext.AuditLog
			.AsNoTracking()
			.OrderByDescending(x => x.CreatedAtUtc)
			.ThenByDescending(x => x.Id)
			.Skip(skip)
			.Take(take)
			.ToArrayAsync(cancellationToken);
}
