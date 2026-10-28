using Domain.Enums;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Models;

namespace Telegram.Service;

/// <summary>
/// Проверка прав пользователя бота. Кнопки старых сообщений остаются рабочими после смены роли
/// или отвязки соединения, поэтому права проверяются при каждом нажатии, а не только при показе кнопки.
/// </summary>
public sealed class TelegramAccessService
{
	private readonly TelegramOptions _telegramOptions;
	private readonly IServiceScopeFactory _scopeFactory;

	public TelegramAccessService(IOptions<TelegramOptions> telegramOptions, IServiceScopeFactory scopeFactory)
	{
		_telegramOptions = telegramOptions.Value;
		_scopeFactory = scopeFactory;
	}

	/// <summary>
	/// Суперадмин из настроек или пользователь с ролью «Администратор»
	/// </summary>
	public async Task<bool> IsAdminAsync(long telegramId, CancellationToken cancellationToken)
	{
		if (_telegramOptions.AdminIds.Contains(telegramId))
		{
			return true;
		}

		using var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

		var user = await repo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
		return user?.Role == UserRole.Administrator;
	}

	/// <summary>
	/// Telegram ID всех администраторов: суперадмины из настроек и пользователи с ролью «Администратор»
	/// </summary>
	public async Task<long[]> GetAdminTelegramIdsAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

		var users = await repo.GetUsersAsync(cancellationToken);
		return users
			.Where(x => x.Role == UserRole.Administrator)
			.Select(x => x.TelegramId)
			.Concat(_telegramOptions.AdminIds)
			.Distinct()
			.ToArray();
	}

	/// <summary>
	/// Может ли пользователь работать с соединением: админ — с любым, остальные — только со своими
	/// </summary>
	public async Task<bool> CanManagePeerAsync(long telegramId, Guid clientId, CancellationToken cancellationToken)
	{
		if (_telegramOptions.AdminIds.Contains(telegramId))
		{
			return true;
		}

		using var scope = _scopeFactory.CreateScope();
		var telegramUserRepo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();
		var vpnUserRepo = scope.ServiceProvider.GetRequiredService<IVpnUserRepository>();

		var user = await telegramUserRepo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
		if (user is null)
		{
			return false;
		}

		if (user.Role == UserRole.Administrator)
		{
			return true;
		}

		var connections = await vpnUserRepo.GetConnectionsByTelegramIdAsync(user.Id, cancellationToken);
		return connections.Any(c => c.Peer.ClientId == clientId);
	}
}
