using Application.Repository;
using Application.Services;
using Application.Services.Interfaces;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DiContainerExtensions
{
	public static void AddApplicationConfiguration(this IServiceCollection services)
	{
		services.AddScoped<ITelegramUserRepository, TelegramUserRepository>();
		services.AddScoped<IVpnPeerRepository, VpnPeerRepository>();
		services.AddScoped<IVpnUserRepository, VpnUserRepository>();
		services.AddScoped<IHostingReminderStateRepository, HostingReminderStateRepository>();
		services.AddScoped<IVpnTrafficSnapshotRepository, VpnTrafficSnapshotRepository>();
		services.AddScoped<IInviteRepository, InviteRepository>();
		services.AddScoped<IAuditLogRepository, AuditLogRepository>();
		services.AddScoped<AuditLogService>();
		services.AddScoped<InviteService>();
		services.AddScoped<PeerSyncService>();
		services.AddScoped<SubscriptionService>();
		services.AddScoped<TelegramUserService>();
		services.AddTransient<IVpnApiService, VpnApiService>();
	}
}