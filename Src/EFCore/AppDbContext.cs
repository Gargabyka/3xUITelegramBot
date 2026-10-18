using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EFCore;

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options)
		: base(options)
	{
	}

	public DbSet<TelegramUser> TelegramUsers { get; set; }

	public DbSet<VpnPeer> VpnPeers { get; set; }

	public DbSet<VpnUser> VpnUsers { get; set; }

	public DbSet<HostingReminderState> HostingReminderStates { get; set; }

	public DbSet<VpnTrafficSnapshot> VpnTrafficSnapshots { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
		base.OnModelCreating(modelBuilder);
	}
}