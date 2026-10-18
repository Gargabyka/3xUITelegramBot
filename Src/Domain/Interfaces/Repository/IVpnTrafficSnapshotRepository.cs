using Domain.Entities;

namespace Domain.Interfaces.Repository;

/// <summary>
/// Интерфейс для работы с <see cref="VpnTrafficSnapshot"/>
/// </summary>
public interface IVpnTrafficSnapshotRepository
{
	/// <summary>
	/// Добавить снимки счётчиков трафика
	/// </summary>
	/// <param name="snapshots">Снимки</param>
	public void AddRange(VpnTrafficSnapshot[] snapshots);

	/// <summary>
	/// Получить снимки для расчёта расхода за период
	/// </summary>
	/// <param name="clientId">Идентификатор клиента 3x-ui</param>
	/// <param name="sinceUtc">Начало периода (null — за всё время)</param>
	/// <param name="cancellationToken">Токен отмены</param>
	/// <returns>
	/// Возвращает снимки за период в хронологическом порядке. Если период ограничен —
	/// первым элементом идёт последний снимок ДО начала периода (нужен как база для расчёта).
	/// </returns>
	public Task<VpnTrafficSnapshot[]> GetSnapshotsForPeriodAsync(
		Guid clientId,
		DateTime? sinceUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// Получить дату самого первого снимка клиента (с какого момента ведётся учёт)
	/// </summary>
	/// <param name="clientId">Идентификатор клиента 3x-ui</param>
	/// <param name="cancellationToken">Токен отмены</param>
	public Task<DateTime?> GetFirstSnapshotDateAsync(Guid clientId, CancellationToken cancellationToken);
}
