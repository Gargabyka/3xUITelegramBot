using Domain.Enums;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Models;

namespace Telegram.Service.Callback.Admin;

/// <summary>
/// Базовый класс для админ панели
/// </summary>
public abstract class TelegramAdminCallbackHandlerBase : TelegramCallbackHandlerBase
{
	private readonly TelegramOptions _telegramOptions;
	private readonly IServiceScopeFactory _scopeFactory;

	protected TelegramAdminCallbackHandlerBase(
		ITelegramBotClient telegramBot,
		IOptions<TelegramOptions> telegramOptions,
		IServiceScopeFactory scopeFactory)
		: base(telegramBot)
	{
		_scopeFactory = scopeFactory;
		_telegramOptions = telegramOptions.Value;
	}

	/// <summary>
	/// Проверяет являться ли пользователь админом
	/// </summary>
	/// <param name="telegramId">Идентификатор телеграма</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>
	/// True - если админ
	/// False - если нет
	/// </returns>
	protected async Task<bool> IsAdminAsync(long telegramId, CancellationToken cancellationToken)
	{
		if (_telegramOptions.AdminIds.Contains(telegramId))
		{
			return true;
		}

		var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetService<ITelegramUserRepository>();
		if (repo is null)
		{
			return false;
		}

		var user = await repo.GetUserByTelegramIdAsync(telegramId, cancellationToken);
		return user?.Role == UserRole.Administrator;
	}
}