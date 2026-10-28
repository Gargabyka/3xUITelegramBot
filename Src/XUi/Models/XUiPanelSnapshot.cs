namespace XUi.Models;

/// <summary>
/// Ответ панели 3x-ui на момент запроса: клиенты всех inbound'ов и время их последнего подключения
/// </summary>
/// <param name="Clients">Клиенты</param>
/// <param name="LastOnline">Время последнего подключения по email (unix-мс)</param>
/// <param name="FetchedAtUtc">Когда получен ответ</param>
public sealed record XUiPanelSnapshot(
	IReadOnlyList<XUiPanelClient> Clients,
	IReadOnlyDictionary<string, long> LastOnline,
	DateTime FetchedAtUtc);
