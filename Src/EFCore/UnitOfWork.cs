using Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace EFCore;

public sealed class UnitOfWork : IUnitOfWork
{
	private readonly AppDbContext _db;

	public UnitOfWork(AppDbContext db)
	{
		_db = db;
	}

	public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		=> _db.SaveChangesAsync(cancellationToken);

	public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
		=> _db.Database.BeginTransactionAsync(cancellationToken);
}