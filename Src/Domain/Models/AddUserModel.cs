namespace Domain.Models;

/// <summary>
/// Модель для добавления пользователя
/// </summary>
/// <param name="TelegramId">Идентификатор телеграма</param>
/// <param name="FirstName">Имя</param>
/// <param name="LastName">Фамилия</param>
/// <param name="UserName">Имя пользователя в телеграм</param>
public record AddUserModel(long TelegramId, string? FirstName, string? LastName, string? UserName);