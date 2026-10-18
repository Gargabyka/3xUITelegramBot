using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;

namespace Domain.Entities;

/// <summary>
/// Пользователи Telegram
/// </summary>
public class TelegramUser : IEntity
{
    /// <summary>
    /// Идентификатор
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Идентификатор телеграма
    /// </summary>
    public long TelegramId { get; init; }

    /// <summary>
    /// Имя
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Фамилия
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Имя пользователя в телеграм
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Роль пользователя
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Дата присоединения к боту
    /// </summary>
    public DateOnly CreatedAt { get; init; }

    /// <summary>
    /// Не присылать напоминания об оплате хостинга
    /// </summary>
    public bool ReminderOptOut { get; set; }

    /// <summary>
    /// Максимальное количество соединений (peer'ов), которое пользователь может завести себе сам.
    /// null — без ограничений.
    /// </summary>
    public int? MaxConnections { get; set; }

    /// <summary>
    /// Добавить пользователя Telegram
    /// </summary>
    /// <param name="userModel">Модель пользователя</param>
    /// <param name="defaultMaxConnections">Лимит соединений по умолчанию для нового пользователя (null — без ограничений)</param>
    /// <returns>Пользователь телеграм</returns>
    public static TelegramUser Create(AddUserModel userModel, int? defaultMaxConnections = null)
    {
	    return new TelegramUser
	    {
		    TelegramId = userModel.TelegramId,
		    FirstName = userModel.FirstName,
		    LastName = userModel.LastName,
		    UserName = userModel.UserName,
		    Role = UserRole.User,
		    CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
		    MaxConnections = defaultMaxConnections,
	    };
    }

    public void Update(string? firstName, string? lastName, string? userName)
    {
	    if (!string.IsNullOrWhiteSpace(firstName) && FirstName != firstName)
	    {
		    FirstName = firstName;
	    }

	    if (!string.IsNullOrWhiteSpace(lastName) && LastName != lastName)
	    {
		    LastName = lastName;
	    }

	    if (!string.IsNullOrWhiteSpace(userName) && UserName != userName)
	    {
		    UserName = userName;
	    }
    }
}