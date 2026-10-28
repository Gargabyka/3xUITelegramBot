using System.Text;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Models;

namespace Telegram.Service;

/// <summary>
/// Проверяет доступность VPN-серверов и сообщает админам, когда сервер перестал отвечать и когда снова заработал
/// </summary>
public sealed class ServerMonitoringBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ITelegramBotClient _bot;
	private readonly TelegramAccessService _access;
	private readonly MonitoringOptions _options;
	private readonly ILogger<ServerMonitoringBackgroundService> _logger;

	// Состояние по профилям: сколько проверок подряд неудачно и когда сервер упал (если админы уже уведомлены)
	private readonly Dictionary<VpnProfileType, int> _failures = new();
	private readonly Dictionary<VpnProfileType, DateTime> _downSinceUtc = new();

	public ServerMonitoringBackgroundService(
		IServiceScopeFactory scopeFactory,
		ITelegramBotClient bot,
		TelegramAccessService access,
		IOptions<MonitoringOptions> options,
		ILogger<ServerMonitoringBackgroundService> logger)
	{
		_scopeFactory = scopeFactory;
		_bot = bot;
		_access = access;
		_options = options.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			return;
		}

		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)));

		do
		{
			try
			{
				await CheckAsync(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				// нормальная остановка
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка мониторинга VPN-серверов");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}

	private static string FormatDuration(TimeSpan duration)
		=> duration.TotalHours >= 1
			? $"{(int)duration.TotalHours} ч {duration.Minutes} мин"
			: $"{Math.Max(1, (int)duration.TotalMinutes)} мин";

	private async Task CheckAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var vpnApiService = scope.ServiceProvider.GetRequiredService<IVpnApiService>();

		var statuses = await vpnApiService.GetStatusAsync(cancellationToken);
		var now = DateTime.UtcNow;

		var down = new List<string>();
		var recovered = new List<string>();

		foreach (var status in statuses)
		{
			var profile = status.Profile.Type;

			if (status.IsAvailable)
			{
				_failures[profile] = 0;
				if (_downSinceUtc.Remove(profile, out var since))
				{
					recovered.Add($"✅ {status.Profile.Title} снова доступен (простой ~{FormatDuration(now - since)})");
				}

				continue;
			}

			var failures = _failures.GetValueOrDefault(profile) + 1;
			_failures[profile] = failures;

			if (failures >= Math.Max(1, _options.FailureThreshold) && !_downSinceUtc.ContainsKey(profile))
			{
				// Отсчёт простоя — с первой неудачной проверки
				var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
				_downSinceUtc[profile] = now - (interval * (failures - 1));
				down.Add($"❌ {status.Profile.Title} не отвечает: {status.Error}");
			}
		}

		if (down.Count == 0 && recovered.Count == 0)
		{
			return;
		}

		var text = new StringBuilder("🛰 Мониторинг серверов\n");
		foreach (var line in down.Concat(recovered))
		{
			text.Append('\n').Append(line);
		}

		await NotifyAdminsAsync(text.ToString(), cancellationToken);
	}

	private async Task NotifyAdminsAsync(string text, CancellationToken cancellationToken)
	{
		var admins = await _access.GetAdminTelegramIdsAsync(cancellationToken);
		foreach (var admin in admins)
		{
			try
			{
				await _bot.SendMessage(admin, text, cancellationToken: cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogWarning(ex, "Не удалось отправить уведомление мониторинга админу {TelegramId}", admin);
			}
		}
	}
}
