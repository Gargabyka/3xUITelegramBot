using System.Security.Cryptography;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Domain.Models;

namespace Application.Services;

/// <summary>
/// Приглашения по ссылке: создание и принятие
/// </summary>
public sealed class InviteService
{
	private readonly IInviteRepository _inviteRepository;
	private readonly ITelegramUserRepository _telegramUserRepository;
	private readonly IUnitOfWork _unitOfWork;

	public InviteService(
		IInviteRepository inviteRepository,
		ITelegramUserRepository telegramUserRepository,
		IUnitOfWork unitOfWork)
	{
		_inviteRepository = inviteRepository;
		_telegramUserRepository = telegramUserRepository;
		_unitOfWork = unitOfWork;
	}

	/// <summary>
	/// Создать одноразовое приглашение
	/// </summary>
	/// <param name="createdByTelegramId">Telegram ID администратора</param>
	/// <param name="lifetime">Срок действия</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Токен для ссылки (только символы A-Z a-z 0-9 _ -)</returns>
	public async Task<string> CreateAsync(long createdByTelegramId, TimeSpan lifetime, CancellationToken cancellationToken)
	{
		var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');

		var now = DateTime.UtcNow;
		_inviteRepository.Add(new Invite
		{
			Token = token,
			CreatedByTelegramId = createdByTelegramId,
			CreatedAtUtc = now,
			ExpiresAtUtc = now + lifetime,
		});

		await _unitOfWork.SaveChangesAsync(cancellationToken);
		return token;
	}

	/// <summary>
	/// Принять приглашение: занять токен и завести пользователя с ролью «Пользователь»
	/// </summary>
	/// <param name="token">Токен из ссылки</param>
	/// <param name="user">Данные пришедшего пользователя</param>
	/// <param name="defaultConnectionLimit">Лимит соединений по умолчанию</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>Приглашение, если принято; null — недействительно, просрочено или уже использовано</returns>
	public async Task<Invite?> TryAcceptAsync(
		string token,
		AddUserModel user,
		int? defaultConnectionLimit,
		CancellationToken cancellationToken)
	{
		var existing = await _telegramUserRepository.GetUserByTelegramIdAsync(user.TelegramId, cancellationToken);
		if (existing is not null)
		{
			return null;
		}

		var invite = await _inviteRepository.TryClaimAsync(token, DateTime.UtcNow, user.TelegramId, cancellationToken);
		if (invite is null)
		{
			return null;
		}

		_telegramUserRepository.Add(TelegramUser.Create(user, defaultConnectionLimit));
		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return invite;
	}
}
