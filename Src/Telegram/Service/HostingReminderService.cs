using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Common.Helper;

namespace Telegram.Service;

/// <summary>
/// Напоминания об оплате хостинга: настройки (в БД, меняются из админ-панели) и рассылка
/// </summary>
public sealed class HostingReminderService
{
	/// <summary>
	/// Варианты периода оплаты, месяцев
	/// </summary>
	public static readonly int[] PeriodPresets = [1, 2, 3, 6, 12];

	// Лимит Telegram — около 30 сообщений в секунду; держимся с запасом
	private static readonly TimeSpan DelayBetweenMessages = TimeSpan.FromMilliseconds(50);

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ITelegramBotClient _bot;
	private readonly ILogger<HostingReminderService> _logger;

	public HostingReminderService(
		IServiceScopeFactory scopeFactory,
		ITelegramBotClient bot,
		ILogger<HostingReminderService> logger)
	{
		_scopeFactory = scopeFactory;
		_bot = bot;
		_logger = logger;
	}

	/// <summary>
	/// Текущие настройки и дата следующего напоминания
	/// </summary>
	public async Task<HostingReminderSettings> GetAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var state = await LoadAsync(scope, cancellationToken);
		await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

		return ToSettings(state);
	}

	/// <summary>
	/// Изменить настройки
	/// </summary>
	/// <param name="change">Изменение</param>
	/// <param name="restartSchedule">
	/// Изменились дата оплаты или период: циклы пересчитываются от новых значений,
	/// и прошедшие сроки не рассылаются задним числом
	/// </param>
	/// <param name="cancellationToken">Токен отмены</param>
	public async Task UpdateAsync(Action<HostingReminderState> change, bool restartSchedule, CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var state = await LoadAsync(scope, cancellationToken);

		change(state);

		if (restartSchedule)
		{
			state.RemindersSentCount = state.LastPaymentDate is { } lastPayment
				? CyclesBefore(lastPayment, state.PeriodMonths, Today())
				: 0;
		}

		await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Разослать напоминание, если наступил срок (вызывается фоновой проверкой)
	/// </summary>
	public async Task SendIfDueAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var state = await LoadAsync(scope, cancellationToken);
		var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

		if (!state.Enabled || state.LastPaymentDate is not { } lastPayment || !HasMessage(state))
		{
			await uow.SaveChangesAsync(cancellationToken);
			return;
		}

		var today = Today();

		// Дата следующего цикла всегда считается от исходной LastPaymentDate (не цепочкой
		// AddMonths от предыдущей даты) — иначе для дат конца месяца дата необратимо "плывёт"
		// на более раннее число после пересечения более короткого месяца (см. HostingReminderScheduler).
		var nextCycle = state.RemindersSentCount + 1;
		if (HostingReminderScheduler.GetDueDate(lastPayment, state.PeriodMonths, nextCycle) > today)
		{
			await uow.SaveChangesAsync(cancellationToken);
			return;
		}

		// Если бот был выключен несколько циклов подряд — не заваливаем напоминаниями за каждый пропущенный
		// цикл, а один раз шлём актуальное и перескакиваем состояние на последний наступивший срок.
		while (HostingReminderScheduler.GetDueDate(lastPayment, state.PeriodMonths, nextCycle + 1) <= today)
		{
			nextCycle++;
		}

		await SendToRecipientsAsync(scope, state, cancellationToken);

		state.RemindersSentCount = nextCycle;
		state.LastSentAtUtc = DateTime.UtcNow;
		await uow.SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Разослать напоминание всем получателям сейчас, вне расписания (расписание не сдвигается)
	/// </summary>
	/// <returns>Сколько доставлено и сколько нет</returns>
	public async Task<(int Sent, int Failed)> SendNowAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var state = await LoadAsync(scope, cancellationToken);
		await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

		return await SendToRecipientsAsync(scope, state, cancellationToken);
	}

	/// <summary>
	/// Прислать напоминание одному получателю (предпросмотр для админа)
	/// </summary>
	/// <returns>false — текст напоминания не задан</returns>
	public async Task<bool> SendPreviewAsync(long chatId, CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var state = await LoadAsync(scope, cancellationToken);
		await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

		if (!HasMessage(state))
		{
			return false;
		}

		await SendReminderAsync(state, chatId, cancellationToken);
		return true;
	}

	/// <summary>
	/// Сколько пользователей получат напоминание и сколько всего пользователей
	/// </summary>
	public async Task<(int Recipients, int Total)> CountRecipientsAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var repo = scope.ServiceProvider.GetRequiredService<ITelegramUserRepository>();

		return ((await repo.GetUsersForReminderAsync(cancellationToken)).Length, (await repo.GetUsersAsync(cancellationToken)).Length);
	}

	private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

	private static bool HasMessage(HostingReminderState state)
		=> state.MessageId is not null || !string.IsNullOrWhiteSpace(state.MessageText);

	/// <summary>
	/// Сколько сроков уже прошло до сегодняшнего дня (срок «сегодня» ещё не считается — он будет разослан)
	/// </summary>
	private static int CyclesBefore(DateOnly lastPayment, int periodMonths, DateOnly today)
	{
		var cycles = 0;
		while (HostingReminderScheduler.GetDueDate(lastPayment, periodMonths, cycles + 1) < today)
		{
			cycles++;
		}

		return cycles;
	}

	private static HostingReminderSettings ToSettings(HostingReminderState state)
		=> new(
			state.Enabled,
			state.LastPaymentDate,
			state.PeriodMonths,
			HasMessage(state),
			state.MessageId is not null,
			state.LastPaymentDate is { } lastPayment
				? HostingReminderScheduler.GetDueDate(lastPayment, state.PeriodMonths, state.RemindersSentCount + 1)
				: null,
			state.LastSentAtUtc);

	/// <summary>
	/// Загружает настройки. До первой настройки в админ-панели напоминания выключены и пусты
	/// </summary>
	private static Task<HostingReminderState> LoadAsync(IServiceScope scope, CancellationToken cancellationToken)
		=> scope.ServiceProvider
			.GetRequiredService<IHostingReminderStateRepository>()
			.GetOrCreateAsync(cancellationToken);

	private async Task<(int Sent, int Failed)> SendToRecipientsAsync(IServiceScope scope, HostingReminderState state, CancellationToken cancellationToken)
	{
		var users = await scope.ServiceProvider
			.GetRequiredService<ITelegramUserRepository>()
			.GetUsersForReminderAsync(cancellationToken);

		var sent = 0;
		var failed = 0;
		foreach (var user in users)
		{
			try
			{
				await SendReminderAsync(state, user.TelegramId, cancellationToken);
				sent++;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// Пользователь мог заблокировать бота — это не повод останавливать рассылку
				failed++;
				_logger.LogWarning(ex, "Не удалось отправить напоминание об оплате хостинга пользователю {TelegramId}", user.TelegramId);
			}

			await Task.Delay(DelayBetweenMessages, cancellationToken);
		}

		return (sent, failed);
	}

	/// <summary>
	/// Копирует сообщение-образец как есть; если его удалили — отправляет запасной текст
	/// </summary>
	private async Task SendReminderAsync(HostingReminderState state, long chatId, CancellationToken cancellationToken)
	{
		if (state.MessageChatId is { } sourceChatId && state.MessageId is { } sourceMessageId)
		{
			try
			{
				await _bot.CopyMessage(chatId, sourceChatId, sourceMessageId, cancellationToken: cancellationToken);
				return;
			}
			catch (Exception ex) when (ex is not OperationCanceledException && !string.IsNullOrWhiteSpace(state.MessageText))
			{
				_logger.LogWarning(ex, "Не удалось скопировать сообщение-образец напоминания, отправляю запасной текст");
			}
		}

		await _bot.SendMessage(chatId, state.MessageText!, cancellationToken: cancellationToken);
	}
}

/// <summary>
/// Настройки напоминаний об оплате для показа
/// </summary>
/// <param name="Enabled">Рассылка включена</param>
/// <param name="LastPaymentDate">Дата последней оплаты</param>
/// <param name="PeriodMonths">Период оплаты, месяцев</param>
/// <param name="HasMessage">Текст напоминания задан</param>
/// <param name="IsCopiedMessage">Напоминание копируется из сообщения-образца (с форматированием и вложениями)</param>
/// <param name="NextDueDate">Дата следующего напоминания (null — не задана дата оплаты)</param>
/// <param name="LastSentAtUtc">Когда напоминание последний раз уходило по расписанию</param>
public sealed record HostingReminderSettings(
	bool Enabled,
	DateOnly? LastPaymentDate,
	int PeriodMonths,
	bool HasMessage,
	bool IsCopiedMessage,
	DateOnly? NextDueDate,
	DateTime? LastSentAtUtc);
