namespace XUi.Models;

/// <summary>
/// Клиент панели 3x-ui (общий для всех inbound'ов, к которым он привязан)
/// </summary>
/// <param name="Id">UUID VLESS</param>
/// <param name="Email">Email клиента — имя соединения</param>
/// <param name="Enabled">Клиент включён</param>
/// <param name="InboundIds">Inbound'ы, к которым привязан клиент</param>
/// <param name="Up">Трафик от клиента, байт</param>
/// <param name="Down">Трафик к клиенту, байт</param>
/// <param name="CreatedAt">Дата создания</param>
/// <param name="SubId">subId ссылки-подписки</param>
public sealed record XUiPanelClient(
	Guid Id,
	string Email,
	bool Enabled,
	int[] InboundIds,
	long Up,
	long Down,
	DateTimeOffset? CreatedAt,
	string? SubId);
