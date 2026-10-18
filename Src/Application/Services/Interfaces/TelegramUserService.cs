using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;

namespace Application.Services.Interfaces;

/// <summary>
/// Сервис для работы с <see cref="TelegramUser"/>
/// </summary>
public sealed class TelegramUserService
{
	private readonly ITelegramUserRepository _telegramUserRepository;
	private readonly IUnitOfWork _unitOfWork;

	public TelegramUserService(ITelegramUserRepository telegramUserRepository, IUnitOfWork unitOfWork)
	{
		_telegramUserRepository = telegramUserRepository;
		_unitOfWork = unitOfWork;
	}

	/// <summary>
	/// Добавить пользователя
	/// </summary>
	/// <param name="userModel">Модель пользователя</param>
	/// <param name="defaultConnectionLimit">Лимит соединений по умолчанию для нового пользователя (null — без ограничений)</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>
	/// True - пользователь успешно создан
	/// False - не удалось создать пользователя
	/// </returns>
	public async Task<bool> TryAddUserAsync(AddUserModel userModel, int? defaultConnectionLimit, CancellationToken cancellationToken)
	{
		var user = await _telegramUserRepository.GetUserByTelegramIdAsync(userModel.TelegramId, cancellationToken);
		if (user is not null)
		{
			return false;
		}

		_telegramUserRepository.Add(TelegramUser.Create(userModel, defaultConnectionLimit));

		await _unitOfWork.SaveChangesAsync(cancellationToken);
		return true;
	}
}