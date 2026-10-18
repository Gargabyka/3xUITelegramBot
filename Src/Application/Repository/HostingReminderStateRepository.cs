using Domain.Entities;
using Domain.Interfaces.Repository;
using EFCore;
using Microsoft.EntityFrameworkCore;

namespace Application.Repository;

public class HostingReminderStateRepository : IHostingReminderStateRepository
{
	private readonly AppDbContext _dbContext;

	public HostingReminderStateRepository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<HostingReminderState> GetOrCreateAsync(CancellationToken cancellationToken)
	{
		var state = await _dbContext.HostingReminderStates.FirstOrDefaultAsync(cancellationToken);
		if (state is not null)
		{
			return state;
		}

		state = new HostingReminderState();
		_dbContext.HostingReminderStates.Add(state);

		return state;
	}
}
