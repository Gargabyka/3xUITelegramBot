namespace Domain.Interfaces;

/// <summary>
/// Интерфейс для работы с сущностью
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Идентификатор сущности
    /// </summary>
    public int Id { get; init; }
}